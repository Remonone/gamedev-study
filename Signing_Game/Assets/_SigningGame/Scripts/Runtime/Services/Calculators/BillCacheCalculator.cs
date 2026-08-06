using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Modifiers;
using Services.Locator;

namespace Services.Calculators {
    public sealed class BillCacheCalculator : ICacheCalculator<BillEntries>, IService, IPreInitialize {
        private IModifierService _modifierService;
        private IAssetProvider _assetProvider;
        private IAssetListLease<BillReference> _referenceLease;
        private BillReference _reference;

        public BillEntries Calculate() {
            return _modifierService.Apply(_reference.Value);
        }

        public async UniTask PreInitializeAsync(IServiceScope scope) {
            _modifierService = scope.Get<IModifierService>();
            _assetProvider = scope.Container.Get<IAssetProvider>();
            _referenceLease = await _assetProvider.LoadAssetsByLabelAsync<BillReference>(
                AddressableConstants.CACHE_REFERENCE_LABEL);
            if (_referenceLease.Assets.Count != 1) {
                throw new System.InvalidOperationException(
                    $"Exactly one BillReference is required, found {_referenceLease.Assets.Count}.");
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
