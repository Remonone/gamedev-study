using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Modifiers;
using Data.Modifiers.Providers;
using Services.Locator;

namespace Services.Calculators {
    public class DocumentCacheCalculator : ICacheCalculator<DocumentEntries>, IService, IPreInitialize {
        private IModifierService _modifierService;
        private IAssetProvider _assetProvider;
        private IAssetListLease<DocumentReference> _referenceLease;
        private DocumentReference _reference;
        private ModifierStorage _modifierStorage;
        
        public void Dispose() {
            _referenceLease.Dispose();
            _referenceLease = null;
            _reference = null;
        }

        public DocumentEntries Calculate() {
            var income = _reference.Value;
            return _modifierService.Apply(income);
        }

        internal DocumentEntries CalculateUpgradeOnly() {
            return _modifierStorage.GetProvider<UpgradeModifierProvider>().Collect(_reference.Value);
        }

        public async UniTask PreInitializeAsync(IServiceScope scope) {
            _modifierService = scope.Get<IModifierService>();
            _modifierStorage = scope.Get<ModifierStorage>();
            _assetProvider = scope.Container.Get<IAssetProvider>();
            _referenceLease = await _assetProvider.LoadAssetsByLabelAsync<DocumentReference>(AddressableConstants.CACHE_REFERENCE_LABEL);
            _reference = _referenceLease.Assets[0];
        }
    }
}
