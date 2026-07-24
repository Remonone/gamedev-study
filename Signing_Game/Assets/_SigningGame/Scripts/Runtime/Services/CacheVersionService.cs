using System;
using System.Collections.Generic;
using Data.Cache;

namespace Services {
    public class CacheVersionService : ICacheVersionProvider, IService, ICacheInvalidator {
        
        private Dictionary<Type, int> _versions = new();
        
        public void Dispose() {
            
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
        }
    }
}