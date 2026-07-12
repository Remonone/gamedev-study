using System.Collections.Generic;

namespace Data.Results {
    public sealed class SignatureVariantMatchResult {
        public string VariantId { get; }
        public float Similarity { get; }

        public SignatureScoreBreakdown Breakdown { get; }

        public IReadOnlyList<SignatureStrokeMatchResult> StrokeResults { get; }
    }
}