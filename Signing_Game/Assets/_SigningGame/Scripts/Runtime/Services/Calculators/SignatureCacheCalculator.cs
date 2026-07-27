using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Modifiers;
using Services.Locator;

namespace Services.Calculators {
    public sealed class SignatureCacheCalculator : ICacheCalculator<SignatureEntries>, IService, IPreInitialize {
        
        private IModifierService _modifierService;
        private IAssetProvider _assetProvider;
        private IAssetListLease<SignatureReference> _referenceLease;
        private SignatureReference _reference;

        public void Dispose() {
            _referenceLease.Dispose();
            _referenceLease = null;
            _reference = null;
        }

        public SignatureEntries Calculate() {
            var value = _reference.Value;
            return _modifierService.Apply(value);
        }

        public async UniTask PreInitializeAsync(IServiceScope scope) {
            _modifierService = scope.Get<IModifierService>();
            _assetProvider = scope.Container.Get<IAssetProvider>();
            _referenceLease = await _assetProvider.LoadAssetsByLabelAsync<SignatureReference>(AddressableConstants.CACHE_REFERENCE_LABEL);
            _reference = _referenceLease.Assets[0];
        }
    }
}