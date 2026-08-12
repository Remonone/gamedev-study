using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Modifiers;
using Data.Modifiers.Providers;
using Services.Locator;

namespace Services.Calculators {
    public sealed class GenerationCacheCalculator : ICacheCalculator<GenerationEntries>, IService, IPreInitialize {
        
        private IModifierService _modifierService;
        private IAssetProvider _assetProvider;
        
        private IAssetListLease<GenerationReference> _referenceLease;
        private GenerationReference _reference;
        private UpgradeModifierProvider _upgradeModifierProvider;
        private PracticeModifierProvider _practiceModifierProvider;

        public void Dispose() {
            _referenceLease.Dispose();
            _referenceLease = null;
            _reference = null;
        }

        public GenerationEntries Calculate() {
            var value = _reference.Value;
            return _modifierService.Apply(value);
        }

        internal GenerationEntries CalculateWithoutBill() {
            GenerationEntries result = _upgradeModifierProvider.Collect(_reference.Value);
            return _practiceModifierProvider != null ? _practiceModifierProvider.Collect(result) : result;
        }

        public async UniTask PreInitializeAsync(IServiceScope scope) {
            _modifierService = scope.Get<IModifierService>();
            _upgradeModifierProvider = scope.Get<ModifierStorage>().GetProvider<UpgradeModifierProvider>();
            scope.Get<ModifierStorage>().TryGetProvider(out _practiceModifierProvider);
            _assetProvider = scope.Container.Get<IAssetProvider>();
            _referenceLease = await _assetProvider.LoadAssetsByLabelAsync<GenerationReference>(AddressableConstants.CACHE_REFERENCE_LABEL);
            _reference = _referenceLease.Assets[0];
        }
    }
}
