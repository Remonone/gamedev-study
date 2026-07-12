namespace Data.Rules {
    public readonly struct SignatureRuleModifiers {
        public readonly float CorridorWidthMultiplier;
        public readonly float MinimumSimilarityOffset;
        public readonly float CoverageRequirementMultiplier;
        public readonly float AlignmentToleranceMultiplier;
        public readonly int AdditionalSmoothingPasses;

        public SignatureRuleModifiers(
        float CorridorWidthMultiplier,
        float MinimumSimilarityOffset,
        float CoverageRequirementMultiplier,
        float AlignmentToleranceMultiplier,
        int AdditionalSmoothingPasses
        ) {
            this.CorridorWidthMultiplier = CorridorWidthMultiplier;
            this.MinimumSimilarityOffset = MinimumSimilarityOffset;
            this.CoverageRequirementMultiplier = CoverageRequirementMultiplier;
            this.AlignmentToleranceMultiplier = AlignmentToleranceMultiplier;
            this.AdditionalSmoothingPasses = AdditionalSmoothingPasses;
        }

        public static SignatureRuleModifiers None => new(
            CorridorWidthMultiplier: 1f,
            MinimumSimilarityOffset: 0f,
            CoverageRequirementMultiplier: 1f,
            AlignmentToleranceMultiplier: 1f,
            AdditionalSmoothingPasses: 0
        );
    }
}