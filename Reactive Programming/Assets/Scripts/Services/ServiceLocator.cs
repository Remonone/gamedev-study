using System;
using System.Collections.Generic;
using UnityEngine;

namespace Components {
    public class ServiceLocator {
        
        private static ServiceLocator _instance;
        
        public static ServiceLocator Instance {
            get {
                if (_instance == null) {
                    _instance = new ServiceLocator();
                } 
                return _instance;
            }
        }

        private Dictionary<Type, IService> _services = new();

        private ServiceLocator() { }

        public T GetService<T>() where T : IService {
            if (_services.TryGetValue(typeof(T), out var service)) {
                return (T)service;
            }
            throw new ArgumentException($"Service of type {typeof(T).Name} not found.");
        }

        public void RegisterService(IService service) {
            if(service == null) throw new ArgumentNullException(nameof(service));
            var key = service.GetType();
            if (_services.ContainsKey(key)) {
                throw new ArgumentException($"Service of type {key.Name} is already registered.");
            }
            _services[key] = service;
        }

        public void UnregisterService<T>() where T : IService {
            var key = typeof(T);
            if (!_services.ContainsKey(key)) {
                return;
            }
            _services.Remove(key);
        }
    }
}
