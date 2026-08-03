using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Modifiers;
using Services.Locator;

namespace Services.Calculators {
    public class DocumentCacheCalculator : ICacheCalculator<DocumentEntries>, IService, IPreInitialize {
        private IModifierService _modifierService;
        private IAssetProvider _assetProvider;
        private IAssetListLease<DocumentReference> _referenceLease;
        private DocumentReference _reference;
        
        public void Dispose() {
            _referenceLease.Dispose();
            _referenceLease = null;
            _reference = null;
        }

        public DocumentEntries Calculate() {
            var income = _reference.Value;
            return _modifierService.Apply(income);
        }

        public async UniTask PreInitializeAsync(IServiceScope scope) {
            _modifierService = scope.Get<IModifierService>();
            _assetProvider = scope.Container.Get<IAssetProvider>();
            _referenceLease = await _assetProvider.LoadAssetsByLabelAsync<DocumentReference>(AddressableConstants.CACHE_REFERENCE_LABEL);
            _reference = _referenceLease.Assets[0];
        }
    }
}