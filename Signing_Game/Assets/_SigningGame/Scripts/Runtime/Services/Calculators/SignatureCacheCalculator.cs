using System;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Modifiers;
using Services.Locator;

namespace Services.Calculators {
    public sealed class SignatureCacheCalculator : ICacheCalculator<SignatureEntries>, IService, IPreInitialize {
        
        private IModifierService _modifierService;
        private SelectedSignatureLoader _signatureLoader;
        private IReadOnlyCacheData<DocumentEntries> _documentData;
        private PlayerStatStash _stash;

        public void Dispose() {
            _signatureLoader = null;
            _documentData = null;
            _stash = null;
        }

        public SignatureEntries Calculate() {
            SignatureEntries baseline = new(_signatureLoader.GetBaseDifficulty());
            ApplyDocumentQualityMinimumSimilarity(ref baseline);
            SignatureEntries result = _modifierService.Apply(baseline);
            result.MinimumSimilarity = Math.Clamp(result.MinimumSimilarity, 0f, 1f);
            return result;
        }

        public UniTask PreInitializeAsync(IServiceScope scope) {
            _modifierService = scope.Get<IModifierService>();
            _signatureLoader = scope.Get<SelectedSignatureLoader>();
            scope.TryGet(out _documentData);
            scope.TryGet(out _stash);
            return UniTask.CompletedTask;
        }

        private void ApplyDocumentQualityMinimumSimilarity(ref SignatureEntries entries) {
            float addition = entries.DocumentQualityMinimumSimilarityAddition;
            if (!IsFinite(addition) || addition <= 0f) return;

            IReadOnlyCacheData<DocumentEntries> documentData = _documentData ?? _stash?.Documents;
            if (documentData == null) return;

            int selectedQualityLevel = Math.Clamp(documentData.Value.SelectedDocumentQualityLevel, 0, 9) + 1;
            entries.MinimumSimilarity += selectedQualityLevel * addition;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
