using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Modifiers;
using Services.Locator;

namespace Services.Calculators {
    public sealed class IncomeCacheCalculator : ICacheCalculator<IncomeEntries>, IService, IPreInitialize {
        
        private IModifierService _modifierService;
        private IAssetProvider _assetProvider;
        private IAssetListLease<IncomeReference> _referenceLease;
        private IncomeReference _reference;
        
        public IncomeEntries Calculate() {
            var income = _reference.Value;
            return _modifierService.Apply(income);
        }
        
        public async UniTask PreInitializeAsync(IServiceScope scope) {
            _modifierService = scope.Get<IModifierService>();
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