using System;
using System.Collections.Generic;

namespace Data.Results {
    public sealed class SignatureVariantMatchResult {
        public string VariantId { get; }
        public float Similarity { get; }

        public SignatureScoreBreakdown Breakdown { get; }

        public IReadOnlyList<SignatureStrokeMatchResult> StrokeResults { get; }

        public SignatureVariantMatchResult(string variantId, float similarity, SignatureScoreBreakdown breakdown,
            IReadOnlyList<SignatureStrokeMatchResult> strokeResults) {
            VariantId = variantId;
            Similarity = similarity;
            Breakdown = breakdown;
            if (strokeResults == null) throw new ArgumentNullException(nameof(strokeResults));
            var snapshot = new SignatureStrokeMatchResult[strokeResults.Count];
            for (int i = 0; i < snapshot.Length; i++) snapshot[i] = strokeResults[i];
            StrokeResults = Array.AsReadOnly(snapshot);
        }
    }
}
