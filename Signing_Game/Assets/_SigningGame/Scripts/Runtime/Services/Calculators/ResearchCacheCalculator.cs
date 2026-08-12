using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Modifiers;
using Services.Locator;

namespace Services.Calculators {
    public sealed class ResearchCacheCalculator : ICacheCalculator<ResearchEntries>, IService, IPreInitialize {
        private IModifierService _modifierService;
        private IAssetListLease<ResearchReference> _referenceLease;
        private ResearchReference _reference;

        public ResearchEntries Calculate() => _modifierService.Apply(_reference.Value);

        public async UniTask PreInitializeAsync(IServiceScope scope) {
            _modifierService = scope.Get<IModifierService>();
            IAssetProvider assets = scope.Container.Get<IAssetProvider>();
            _referenceLease = await assets.LoadAssetsByLabelAsync<ResearchReference>(
                AddressableConstants.CACHE_REFERENCE_LABEL);
            if (_referenceLease.Assets.Count != 1) {
                throw new System.InvalidOperationException(
                    $"Exactly one ResearchReference is required, found {_referenceLease.Assets.Count}.");
            }
            _reference = _referenceLease.Assets[0];
        }

        public void Dispose() {
            _referenceLease?.Dispose();
            _referenceLease = null;
            _reference = null;
        }
    }
}
