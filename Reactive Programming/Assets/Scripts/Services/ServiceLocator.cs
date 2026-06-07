using System;
using System.Collections.Generic;
using System.Linq;

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

        private readonly Dictionary<Type, List<IService>> _services = new();

        private ServiceLocator() {
        }

        public T GetService<T>(int index = 0) where T : class, IService {
            var services = GetServicesInternal<T>();

            if (index < 0 || index >= services.Count) {
                throw new ArgumentOutOfRangeException(
                    nameof(index),
                    $"Service of type {typeof(T).Name} exists, but index {index} is out of range."
                );
            }

            return services[index];
        }

        public IReadOnlyList<T> GetServices<T>() where T : class, IService {
            return GetServicesInternal<T>();
        }

        public bool TryGetService<T>(out T service, int index = 0) where T : class, IService {
            var services = GetServicesInternal<T>();

            if (index < 0 || index >= services.Count) {
                service = null;
                return false;
            }

            service = services[index];
            return true;
        }

        public void RegisterService<T>(T service) where T : class, IService {
            if (service == null) throw new ArgumentNullException(nameof(service));

            var key = service.GetType();

            if (_services.TryGetValue(key, out var services)) {
                if (services.Contains(service)) {
                    throw new ArgumentException($"Service instance of type {key.Name} is already registered.");
                }

                services.Add(service);
            }
            else {
                _services.Add(key, new List<IService> { service });
            }
        }

        public void UnregisterService<T>(T service) where T : class, IService {
            if (service == null) throw new ArgumentNullException(nameof(service));

            var key = service.GetType();

            if (!_services.TryGetValue(key, out var services)) {
                return;
            }

            services.Remove(service);

            if (services.Count == 0) {
                _services.Remove(key);
            }
        }

        public void UnregisterServices<T>() where T : class, IService {
            var targetType = typeof(T);

            var keysToRemove = _services
                .Where(pair => targetType.IsAssignableFrom(pair.Key))
                .Select(pair => pair.Key)
                .ToList();

            foreach (var key in keysToRemove) {
                _services.Remove(key);
            }
        }

        private List<T> GetServicesInternal<T>() where T : class, IService {
            var targetType = typeof(T);

            var result = _services
                .Where(pair => targetType.IsAssignableFrom(pair.Key))
                .SelectMany(pair => pair.Value)
                .Cast<T>()
                .ToList();

            if (result.Count == 0) {
                throw new ArgumentException($"Service of type {targetType.Name} not found.");
            }

            return result;
        }
    }
}
