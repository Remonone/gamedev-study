using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Data.Modifiers.Providers;
using Services.Locator;

namespace Services {
    public class ModifierStorage : IService, IPostInitialize {

        private readonly Dictionary<Type, IModifierProvider> _providers;


        public ModifierStorage() {
            _providers = new();
        }
        
        public IEnumerable<IModifierProvider> Providers => _providers.Values;

        public void RegisterProvider(IModifierProvider provider) {
            var key = provider.GetType();
            if (_providers.ContainsKey(key))
                throw new ArgumentException($"Provider of type {key} already registered");
            _providers.Add(key, provider);
        }

        public T GetProvider<T>() where T : IModifierProvider {
            var targetType = typeof(T);

            var result = _providers
                .Where(pair => targetType.IsAssignableFrom(pair.Key))
                .Select(pair => pair.Value)
                .Cast<T>()
                .ToList();
            
            if (result.Count == 0)
                throw new ArgumentException($"No provider of type {typeof(T)} found");

            return result[0];
        }
        
        public void Dispose() {
            
        }

        public UniTask PostInitializeAsync(IServiceScope scope) {
            foreach (var provider in _providers.Values) {
                provider.Init(scope);
            }
            return UniTask.CompletedTask;
        }
    }
}
