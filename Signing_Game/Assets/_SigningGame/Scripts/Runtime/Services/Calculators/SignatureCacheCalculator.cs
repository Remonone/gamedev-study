using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Modifiers;
using Services.Locator;

namespace Services.Calculators {
    public sealed class SignatureCacheCalculator : ICacheCalculator<SignatureEntries>, IService, IPreInitialize {
        
        private IModifierService _modifierService;
        private SelectedSignatureLoader _signatureLoader;

        public void Dispose() {
            _signatureLoader = null;
        }

        public SignatureEntries Calculate() {
            SignatureEntries baseline = new(_signatureLoader.GetBaseDifficulty());
            return _modifierService.Apply(baseline);
        }

        public UniTask PreInitializeAsync(IServiceScope scope) {
            _modifierService = scope.Get<IModifierService>();
            _signatureLoader = scope.Get<SelectedSignatureLoader>();
            return UniTask.CompletedTask;
        }
    }
}
