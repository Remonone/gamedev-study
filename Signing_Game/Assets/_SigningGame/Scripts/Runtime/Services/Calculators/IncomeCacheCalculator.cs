using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Modifiers;
using Data.Modifiers.Providers;
using Services.Locator;

namespace Services.Calculators {
    public sealed class IncomeCacheCalculator : ICacheCalculator<IncomeEntries>, IService, IPreInitialize {
        
        private IModifierService _modifierService;
        private IAssetProvider _assetProvider;
        private IAssetListLease<IncomeReference> _referenceLease;
        private IncomeReference _reference;
        private UpgradeModifierProvider _upgradeModifierProvider;
        
        public IncomeEntries Calculate() {
            var income = _reference.Value;
            return _modifierService.Apply(income);
        }

        internal IncomeEntries CalculateUpgradeOnly() {
            return _upgradeModifierProvider.Collect(_reference.Value);
        }
        
        public async UniTask PreInitializeAsync(IServiceScope scope) {
            _modifierService = scope.Get<IModifierService>();
            _upgradeModifierProvider = scope.Get<ModifierStorage>().GetProvider<UpgradeModifierProvider>();
            _assetProvider = scope.Container.Get<IAssetProvider>();
            _referenceLease = await _assetProvider.LoadAssetsByLabelAsync<IncomeReference>(AddressableConstants.CACHE_REFERENCE_LABEL);
            _reference = _referenceLease.Assets[0];
        }

        public void Dispose() {
            _referenceLease.Dispose();
            _referenceLease = null;
            _reference = null;
        }
    }
}
