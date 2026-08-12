using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Data.Modifiers.Providers;
using Services.Locator;

namespace Services {
    public class ModifierStorage : IService, IPostInitialize {

        private readonly Dictionary<Type, IModifierProvider> _providersByType;
        private readonly List<IModifierProvider> _providers;


        public ModifierStorage() {
            _providersByType = new();
            _providers = new();
        }
        
        public IReadOnlyList<IModifierProvider> Providers => _providers;

        public void RegisterProvider(IModifierProvider provider) {
            var key = provider.GetType();
            if (_providersByType.ContainsKey(key))
                throw new ArgumentException($"Provider of type {key} already registered");
            _providersByType.Add(key, provider);
            _providers.Add(provider);
        }

        public T GetProvider<T>() where T : class, IModifierProvider {
            if (TryGetProvider(out T result)) return result;
            throw new ArgumentException($"No provider of type {typeof(T)} found");
        }

        public bool TryGetProvider<T>(out T result) where T : class, IModifierProvider {
            for (int index = 0; index < _providers.Count; index++) {
                if (_providers[index] is T typed) {
                    result = typed;
                    return true;
                }
            }
            result = null;
            return false;
        }
        
        public void Dispose() {
            
        }

        public UniTask PostInitializeAsync(IServiceScope scope) {
            for (int index = 0; index < _providers.Count; index++) _providers[index].Init(scope);
            return UniTask.CompletedTask;
        }
    }
}
