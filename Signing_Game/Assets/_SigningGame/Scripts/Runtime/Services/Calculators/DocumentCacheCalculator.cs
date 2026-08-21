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
        private DocumentQualityService _qualityService;
        
        public void Dispose() {
            _referenceLease.Dispose();
            _referenceLease = null;
            _reference = null;
        }

        public DocumentEntries Calculate() {
            var income = _reference.Value;
            return ApplySelectedQuality(_modifierService.Apply(income));
        }

        internal DocumentEntries CalculateWithoutBill() {
            DocumentEntries result = _modifierStorage.GetProvider<UpgradeModifierProvider>().Collect(_reference.Value);
            if (_modifierStorage.TryGetProvider(out MetaUpgradeModifierProvider meta)) result = meta.Collect(result);
            result = _modifierStorage.TryGetProvider(out PracticeModifierProvider practice)
                ? practice.Collect(result)
                : result;
            return ApplySelectedQuality(result);
        }

        public async UniTask PreInitializeAsync(IServiceScope scope) {
            _modifierService = scope.Get<IModifierService>();
            _modifierStorage = scope.Get<ModifierStorage>();
            scope.TryGet(out _qualityService);
            _assetProvider = scope.Container.Get<IAssetProvider>();
            _referenceLease = await _assetProvider.LoadAssetsByLabelAsync<DocumentReference>(AddressableConstants.CACHE_REFERENCE_LABEL);
            _reference = _referenceLease.Assets[0];
        }

        private DocumentEntries ApplySelectedQuality(DocumentEntries entries) {
            if (_qualityService != null) {
                entries.SelectedDocumentQualityLevel = _qualityService.SelectedQualityLevel;
            }
            return entries;
        }
    }
}
