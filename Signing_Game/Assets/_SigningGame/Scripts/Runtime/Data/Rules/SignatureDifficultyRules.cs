namespace Data.Rules {
    public record SignatureDifficultyRules(
        string Id,
        float MinimumSimilarity,
        float CorridorWidthMultiplier,
        float CoverageRequirementMultiplier,
        float AlignmentToleranceMultiplier,
        SignatureScoreWeights ScoreWeights
    );
}