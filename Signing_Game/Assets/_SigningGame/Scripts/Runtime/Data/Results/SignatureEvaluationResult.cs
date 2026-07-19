using System;
using System.Collections.Generic;
using Data.Enums;

namespace Data.Results {
    public sealed class SignatureEvaluationResult {
        private static readonly IReadOnlyList<SignatureStrokeMatchResult> EmptyStrokeResults =
            Array.AsReadOnly(Array.Empty<SignatureStrokeMatchResult>());
        public SignatureEvaluationStatus Status { get; }
        public SignatureFailureReason FailureReason { get; }

        public bool Accepted =>
            Status == SignatureEvaluationStatus.Accepted;

        public float Similarity { get; }
        public float Quality { get; }

        public float MinimumSimilarity { get; }

        public string MatchedVariantId { get; }

        public SignatureScoreBreakdown ScoreBreakdown { get; }
        public SignatureScoreBreakdown Breakdown => ScoreBreakdown;
        public IReadOnlyList<SignatureStrokeMatchResult> StrokeResults { get; }

        public SignatureEvaluationResult(SignatureEvaluationStatus status, SignatureFailureReason failureReason,
            float similarity, float quality, float minimumSimilarity, string matchedVariantId,
            SignatureScoreBreakdown scoreBreakdown, IReadOnlyList<SignatureStrokeMatchResult> strokeResults) {
            Status = status;
            FailureReason = failureReason;
            Similarity = similarity;
            Quality = quality;
            MinimumSimilarity = minimumSimilarity;
            MatchedVariantId = matchedVariantId;
            ScoreBreakdown = scoreBreakdown;
            if (strokeResults == null) throw new ArgumentNullException(nameof(strokeResults));
            if (strokeResults.Count == 0) {
                StrokeResults = EmptyStrokeResults;
            } else {
                var snapshot = new SignatureStrokeMatchResult[strokeResults.Count];
                for (int i = 0; i < snapshot.Length; i++) snapshot[i] = strokeResults[i];
                StrokeResults = Array.AsReadOnly(snapshot);
            }
        }
    }
}
