using System;
using System.Collections.Generic;
using Data.Cache;
using R3;

namespace Services {
    public class CacheVersionService : ICacheVersionProvider, IService, ICacheInvalidator {
        private static readonly Dictionary<Type, Type[]> DependentTypes = new() {
            [typeof(DocumentEntries)] = new[] { typeof(SignatureEntries) }
        };
        
        private readonly Dictionary<Type, int> _versions = new();
        private readonly Subject<Type> _invalidated = new();

        public Observable<Type> Invalidated => _invalidated;
        
        public void Dispose() {
            _invalidated.Dispose();
            _versions.Clear();
        }

        public int GetVersion<T>() {
            if (!_versions.TryGetValue(typeof(T), out var version)) {
                version = 0;
                _versions.Add(typeof(T), version);
            }
            return version;
        }

        void ICacheInvalidator.Invalidate<T>() {
            InvalidateType(typeof(T));
        }
        
        void ICacheInvalidator.InvalidateAll() {
            var knownTypes = new List<Type>(_versions.Keys);
            foreach (var type in knownTypes) {
                _versions[type]++;
                _invalidated.OnNext(type);
            }
        }
        void ICacheInvalidator.Invalidate(Type type) {
            if (type == null) throw new ArgumentNullException(nameof(type));
            InvalidateType(type);
        }

        private void InvalidateType(Type type) {
            InvalidateType(type, new HashSet<Type>());
        }

        private void InvalidateType(Type type, HashSet<Type> visited) {
            if (!visited.Add(type)) return;
            if (!_versions.TryGetValue(type, out int version)) version = 0;
            _versions[type] = version + 1;
            _invalidated.OnNext(type);

            if (!DependentTypes.TryGetValue(type, out Type[] dependents)) return;
            foreach (Type dependent in dependents) {
                InvalidateType(dependent, visited);
            }
        }
    }
}
