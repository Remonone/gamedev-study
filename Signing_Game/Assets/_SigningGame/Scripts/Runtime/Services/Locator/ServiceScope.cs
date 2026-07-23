using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Services.Locator {
    public class ServiceScope : IServiceScope, IDisposable {
        private readonly ServiceLocator _container;
        private readonly Dictionary<Type, List<IService>> _services = new();
        private readonly List<object> _ordered = new();
        private readonly HashSet<object> _orderedSet = new(ReferenceComparer.Instance);
        
        public ServiceLocator Container => _container;

        public ServiceScope(ServiceLocator container) {
            _container = container;
        }

        public ServiceScope Register<T>(T service) where T : class {
            IService validatedService = ValidateService(service, nameof(service));
            Type contractType = typeof(T);
            Type concreteType = service.GetType();

            if (contractType == concreteType || contractType == typeof(IService)) {
                RegisterImplicit(validatedService, concreteType);
            } else {
                ValidateContract(contractType, service, nameof(service));
                AddMapping(contractType, validatedService);
                AddMapping(concreteType, validatedService);
            }

            TrackOrder(validatedService);
            return this;
        }

        public ServiceScope Register(Type type, object service) {
            IService validatedService = ValidateService(service, nameof(service));
            ValidateContract(type, service, nameof(type));

            AddMapping(type, validatedService);
            AddMapping(service.GetType(), validatedService);
            TrackOrder(validatedService);
            return this;
        }

        public ServiceScope Register(object service, params Type[] contracts) {
            IService validatedService = ValidateService(service, nameof(service));
            if (contracts == null) throw new ArgumentNullException(nameof(contracts));

            var uniqueContracts = new HashSet<Type>();
            var validatedContracts = new List<Type>();
            foreach (Type contract in contracts) {
                ValidateContract(contract, service, nameof(contracts));
                if (uniqueContracts.Add(contract)) validatedContracts.Add(contract);
            }

            foreach (Type contract in validatedContracts) AddMapping(contract, validatedService);
            AddMapping(service.GetType(), validatedService);
            TrackOrder(validatedService);
            return this;
        }

        public bool TryGet<T>(out T service, int index = 0) where T : class {
            if (index < 0 || !_services.TryGetValue(typeof(T), out List<IService> services) ||
                index >= services.Count) {
                service = default;
                return false;
            }

            service = (T)(object)services[index];
            return true;
        }

        public T Get<T>(int index = 0) where T : class {
            if (TryGet(out T service, index)) return service;
            throw new InvalidOperationException(
                $"Service contract '{typeof(T)}' has no registration at index {index} in this scope.");
        }

        public async Awaitable PreInitializeAsync(IServiceScope scope) {
            foreach (var service in _ordered) {
                if (service is IPreInitialize preInitialize) {
                    await preInitialize.PreInitializeAsync(scope);
                }
            }
        }

        public async Awaitable InitializeAsync(IServiceScope scope) {
            foreach (var service in _ordered) {
                if (service is IInitialize initialize) {
                    await initialize.InitializeAsync(scope);
                }
            }
        }

        public async Awaitable PostInitializeAsync(IServiceScope scope) {
            foreach (var service in _ordered) {
                if (service is IPostInitialize postInitialize) {
                    await postInitialize.PostInitializeAsync(scope);
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

        private void RegisterImplicit(IService service, Type concreteType) {
            AddMapping(concreteType, service);

            for (Type baseType = concreteType.BaseType; baseType != null; baseType = baseType.BaseType) {
                if (typeof(IService).IsAssignableFrom(baseType)) AddMapping(baseType, service);
            }

            foreach (Type interfaceType in concreteType.GetInterfaces()) {
                if (!IsExcludedImplicitInterface(interfaceType)) AddMapping(interfaceType, service);
            }
        }

        private void AddMapping(Type contract, IService service) {
            if (!_services.TryGetValue(contract, out List<IService> services)) {
                services = new List<IService>();
                _services.Add(contract, services);
            }

            foreach (IService registeredService in services) {
                if (ReferenceEquals(registeredService, service)) return;
            }

            services.Add(service);
        }

        private void TrackOrder(IService service) {
            if (_orderedSet.Add(service)) {
                _ordered.Add(service);
            }
        }

        private static IService ValidateService(object service, string parameterName) {
            if (service == null) throw new ArgumentNullException(parameterName);
            if (service is not IService validatedService) {
                throw new ArgumentException("Service instance must implement IService.", parameterName);
            }
            if (!service.GetType().IsClass) {
                throw new ArgumentException("Service runtime type must be a class.", parameterName);
            }

            return validatedService;
        }

        private static void ValidateContract(Type contract, object service, string parameterName) {
            if (contract == null) throw new ArgumentNullException(parameterName);
            if (!contract.IsClass && !contract.IsInterface) {
                throw new ArgumentException($"Service contract '{contract}' must be a class or interface.", parameterName);
            }
            if (!contract.IsInstanceOfType(service)) {
                throw new ArgumentException(
                    $"Service of runtime type '{service.GetType()}' is not assignable to contract '{contract}'.",
                    parameterName);
            }
        }

        private static bool IsExcludedImplicitInterface(Type interfaceType) {
            return interfaceType == typeof(IService) ||
                   interfaceType == typeof(IDisposable) ||
                   interfaceType == typeof(IPreInitialize) ||
                   interfaceType == typeof(IInitialize) ||
                   interfaceType == typeof(IPostInitialize);
        }

        private sealed class ReferenceComparer : IEqualityComparer<object> {
            public static readonly ReferenceComparer Instance = new();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
