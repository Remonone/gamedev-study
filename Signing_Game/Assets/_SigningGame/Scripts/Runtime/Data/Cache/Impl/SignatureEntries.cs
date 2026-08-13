using System;
using Data.Rules;
using Utils.Attributes;

namespace Data.Cache {
    
    [Serializable, CacheEntryGroup("Signature")]
    public struct SignatureEntries {
        [ModifiableParameter("Minimum Similarity", Minimum = 0d, Maximum = 1d)]
        public float MinimumSimilarity;
        public float DocumentQualityMinimumSimilarityAddition;
        [ModifiableParameter("Corridor Width Multiplier", Minimum = float.Epsilon)]
        public float CorridorWidthMultiplier;
        [ModifiableParameter("Coverage Requirement Multiplier", Minimum = 0d)]
        public float CoverageRequirementMultiplier;
        [ModifiableParameter("Alignment Tolerance Multiplier", Minimum = 0d)]
        public float AlignmentToleranceMultiplier;
        [ModifiableParameter("Corridor Fit Weight", Minimum = 0d)]
        public float CorridorFitWeight;
        [ModifiableParameter("Coverage Weight", Minimum = 0d)]
        public float CoverageWeight;
        [ModifiableParameter("Direction Weight", Minimum = 0d)]
        public float DirectionWeight;
        [ModifiableParameter("Stroke Structure Weight", Minimum = 0d)]
        public float StrokeStructureWeight;

        public SignatureEntries(SignatureDifficultyRules rules) {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (rules.ScoreWeights == null) throw new ArgumentException("Difficulty score weights are required.", nameof(rules));

            MinimumSimilarity = rules.MinimumSimilarity;
            DocumentQualityMinimumSimilarityAddition = rules.DocumentQualityMinimumSimilarityAddition;
            CorridorWidthMultiplier = rules.CorridorWidthMultiplier;
            CoverageRequirementMultiplier = rules.CoverageRequirementMultiplier;
            AlignmentToleranceMultiplier = rules.AlignmentToleranceMultiplier;
            CorridorFitWeight = rules.ScoreWeights.CorridorFit;
            CoverageWeight = rules.ScoreWeights.Coverage;
            DirectionWeight = rules.ScoreWeights.Direction;
            StrokeStructureWeight = rules.ScoreWeights.StrokeStructure;
        }

        public SignatureDifficultyRules ToRules(string profileId) => new(
            profileId,
            MinimumSimilarity,
            CorridorWidthMultiplier,
            CoverageRequirementMultiplier,
            AlignmentToleranceMultiplier,
            new SignatureScoreWeights(CorridorFitWeight, CoverageWeight, DirectionWeight, StrokeStructureWeight)) {
            DocumentQualityMinimumSimilarityAddition = DocumentQualityMinimumSimilarityAddition
        };
    }
}
