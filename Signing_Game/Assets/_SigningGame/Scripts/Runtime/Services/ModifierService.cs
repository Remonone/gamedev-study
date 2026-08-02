using Cysharp.Threading.Tasks;
using Data.Modifiers;
using Services.Locator;

namespace Services {
    public class ModifierService : IModifierService, IService, IInitialize {

        private ModifierStorage _storage;

        public T Apply<T>(T value) where T : struct {
            var result = value;
            foreach (var provider in _storage.Providers) {
                result = provider.Collect(result);
            }
            return result;
        }
        
        
        public void Dispose() {
            
        }

        public UniTask InitializeAsync(IServiceScope scope) {
            _storage = scope.Get<ModifierStorage>();
            return UniTask.CompletedTask;
        }
    }
}
