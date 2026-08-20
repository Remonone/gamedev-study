using System;
using System.Collections.Generic;
using Data.Enums;
using Data.Rules;

namespace Data.Templates {
    public class CompiledSignaturePreset {
        public string Id { get; }
        public string ProcessingProfileId { get; }
        public SignatureProcessingRules Processing { get; }

        public SignatureStrokeMatchMode StrokeMatchMode { get; }

        public int GuidanceFullDisplayAfterSignatures { get; }
        public int GuidanceFadeOutSignatureCount { get; }

        public SignatureAlignmentRules Alignment { get; }

        public IReadOnlyList<SignatureTemplateVariant> Variants { get; }

        public CompiledSignaturePreset(string id, string processingProfileId,
            SignatureProcessingRules processing, SignatureAlignmentRules alignment,
            SignatureStrokeMatchMode strokeMatchMode, IReadOnlyList<SignatureTemplateVariant> variants) {
            if (variants == null) throw new ArgumentNullException(nameof(variants));
            Id = id;
            ProcessingProfileId = processingProfileId;
            Processing = processing ?? throw new ArgumentNullException(nameof(processing));
            Alignment = alignment ?? throw new ArgumentNullException(nameof(alignment));
            StrokeMatchMode = strokeMatchMode;
            GuidanceFullDisplayAfterSignatures = 3;
            GuidanceFadeOutSignatureCount = 5;
            var snapshot = new SignatureTemplateVariant[variants.Count];
            for (int i = 0; i < snapshot.Length; i++) snapshot[i] = variants[i];
            Variants = Array.AsReadOnly(snapshot);
        }

        public CompiledSignaturePreset(string id, string processingProfileId,
            SignatureProcessingRules processing, SignatureAlignmentRules alignment,
            SignatureStrokeMatchMode strokeMatchMode, int guidanceFullDisplayAfterSignatures,
            int guidanceFadeOutSignatureCount, IReadOnlyList<SignatureTemplateVariant> variants) {
            if (guidanceFullDisplayAfterSignatures < 0)
                throw new ArgumentOutOfRangeException(nameof(guidanceFullDisplayAfterSignatures));
            if (guidanceFadeOutSignatureCount < 0)
                throw new ArgumentOutOfRangeException(nameof(guidanceFadeOutSignatureCount));

            GuidanceFullDisplayAfterSignatures = guidanceFullDisplayAfterSignatures;
            GuidanceFadeOutSignatureCount = guidanceFadeOutSignatureCount;
            if (variants == null) throw new ArgumentNullException(nameof(variants));
            Id = id;
            ProcessingProfileId = processingProfileId;
            Processing = processing ?? throw new ArgumentNullException(nameof(processing));
            Alignment = alignment ?? throw new ArgumentNullException(nameof(alignment));
            StrokeMatchMode = strokeMatchMode;
            if (variants == null) throw new ArgumentNullException(nameof(variants));
            var snapshot = new SignatureTemplateVariant[variants.Count];
            for (int i = 0; i < snapshot.Length; i++) snapshot[i] = variants[i];
            Variants = Array.AsReadOnly(snapshot);
        }
    }
}
