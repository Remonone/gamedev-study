using System;
using System.Collections.Generic;
using UnityEngine;

namespace Services.Locator {
    public class ServiceScope : IDisposable {
        private readonly Dictionary<Type, object> _services = new();
        private readonly List<object> _ordered = new();
        private readonly HashSet<object> _orderedSet = new();

        public ServiceScope Register<T>(T service) {
            Type type = typeof(T);
            if (!_services.TryAdd(type, service)) {
                throw new InvalidOperationException($"Service of type {type} already registered.");
            }
            TrackOrder(service);
            return this;
        }

        public ServiceScope Register(Type type, object service) {
            if (!type.IsInstanceOfType(service)) {
                throw new ArgumentException($"Service is a {service.GetType()}. Provided type is {type}.", nameof(service));
            }
            if (!_services.TryAdd(type, service)) {
                throw new InvalidOperationException($"Service of type {type} already registered.");
            }
            TrackOrder(service);
            return this;
        }

        public bool TryGet<T>(out T service) where T : class {
            Type type = typeof(T);

            if (_services.TryGetValue(type, out object obj)) {
                service = obj as T;
                return true;
            }
            service = null;
            return false;
        }

        public T Get<T>() where T : class {
            Type type = typeof(T);

            if (_services.TryGetValue(type, out object service)) {
                return service as T;
            }

            throw new ArgumentException($"Service of type {type} not registered.");
        }

        public async Awaitable PreInitializeAsync(ServiceLocator container) {
            foreach (var service in _ordered) {
                if (service is IPreInitialize preInitialize) {
                    await preInitialize.PreInitializeAsync(container);
                }
            }
        }

        public async Awaitable InitializeAsync(ServiceLocator container) {
            foreach (var service in _ordered) {
                if (service is IInitialize initialize) {
                    await initialize.InitializeAsync(container);
                }
            }
        }

        public async Awaitable PostInitializeAsync(ServiceLocator container) {
            foreach (var service in _ordered) {
                if (service is IPostInitialize postInitialize) {
                    await postInitialize.PostInitializeAsync(container);
                }
            }
        }

        public void Dispose() {
            for (int i = _ordered.Count - 1; i >= 0; i--) {
                if (_ordered[i] is IService disposable) {
                    disposable.Dispose();
                }
            }
            _services.Clear();
            _ordered.Clear();
            _orderedSet.Clear();
        }

        private void TrackOrder(object service) {
            if (service != null && _orderedSet.Add(service)) {
                _ordered.Add(service);
            }
        }
    }
}
