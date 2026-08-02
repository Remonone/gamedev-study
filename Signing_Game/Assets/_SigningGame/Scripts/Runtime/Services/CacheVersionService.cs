using System;
using System.Collections.Generic;
using Data.Cache;
using R3;

namespace Services {
    public class CacheVersionService : ICacheVersionProvider, IService, ICacheInvalidator {
        
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
            if (!_versions.ContainsKey(typeof(T))) return;
            _versions[typeof(T)]++;
            _invalidated.OnNext(typeof(T));
        }
        
        void ICacheInvalidator.InvalidateAll() {
            foreach (var type in _versions.Keys) {
                _versions[type]++;
                _invalidated.OnNext(type);
            }
        }
        void ICacheInvalidator.Invalidate(Type type) {
            if (!_versions.ContainsKey(type)) return;
            _versions[type]++;
            _invalidated.OnNext(type);
        }
    }
}
