using System;
using System.Collections.Generic;
using System.Linq;
using Economy.Providers;
using Types.Buildings;
using Types.Economy;
using Types.Economy.Modifiers;

namespace Services {
    public class ProviderRegistryService : IService {
        
        private Dictionary<Type, IModifierProvider> _providers = new();


        public ProviderRegistryService() {
            
        }

        public void RegisterProvider(IModifierProvider provider) {
            var key = provider.GetType();
            if (_providers.ContainsKey(key)) {
                throw new ArgumentException(nameof(provider) + " is already registered");
            }
            _providers.Add(key, provider);
        }

        public void FetchModifiers(SessionContext context, BuildingState buildingState, List<StatModifier> output) {
            foreach (var provider in _providers.Values) {
                provider.Collect(context, buildingState, output);
            }
        }
        
        public T GetProvider<T>() where T : class, IModifierProvider {
            var targetType = typeof(T);

            var result = _providers
                .Where(pair => targetType.IsAssignableFrom(pair.Key))
                .Select(pair => pair.Value)
                .Cast<T>()
                .ToList();

            if (result.Count == 0) {
                throw new ArgumentException($"Service of type {targetType.Name} not found.");
            }

            return result[0];
        }
    }
}