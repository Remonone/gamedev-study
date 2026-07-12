using Data.Enums;

namespace Data.Rules {
    public record ResolvedSignatureRules(
        float MinimumSimilarity,
        float CorridorWidthMultiplier,
        float CoverageRequirementMultiplier,
        SignatureProcessingRules Processing,
        SignatureAlignmentRules Alignment,
        SignatureScoreWeights ScoreWeights,
        SignatureStrokeMatchMode StrokeMatchMode
    );
}