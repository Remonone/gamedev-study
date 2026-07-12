using Data.Enums;

namespace Data.Results {
    public sealed class SignatureEvaluationResult {
        public SignatureEvaluationStatus Status { get; }
        public SignatureFailureReason FailureReason { get; }

        public bool Accepted =>
            Status == SignatureEvaluationStatus.Accepted;

        public float Similarity { get; }
        public float Quality { get; }

        public float MinimumSimilarity { get; }

        public string MatchedVariantId { get; }

        public SignatureScoreBreakdown Breakdown { get; }
    }
}