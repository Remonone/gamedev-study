using System;
using System.Collections.Generic;
using Data.Enums;

namespace Data.Results {
    public sealed class SignatureEvaluationResult {
        public SignatureEvaluationStatus Status { get; }
        public SignatureFailureReason FailureReason { get; }

        public float Similarity { get; }

        public float MinimumSimilarity { get; }

        public SignatureScoreBreakdown ScoreBreakdown { get; }

        public SignatureEvaluationResult(SignatureEvaluationStatus status, SignatureFailureReason failureReason,
            float similarity, float minimumSimilarity, SignatureScoreBreakdown scoreBreakdown) {
            Status = status;
            FailureReason = failureReason;
            Similarity = similarity;
            MinimumSimilarity = minimumSimilarity;
            ScoreBreakdown = scoreBreakdown;
        }
    }
}
