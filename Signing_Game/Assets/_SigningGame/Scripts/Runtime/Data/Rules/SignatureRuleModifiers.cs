namespace Data.Rules {
    public readonly struct SignatureRuleModifiers {
        public readonly float CorridorWidthMultiplier;
        public readonly float MinimumSimilarityOffset;
        public readonly float CoverageRequirementMultiplier;
        public readonly float AlignmentToleranceMultiplier;
        public readonly float DirectionContributionMultiplier;

        public SignatureRuleModifiers(
        float CorridorWidthMultiplier,
        float MinimumSimilarityOffset,
        float CoverageRequirementMultiplier,
        float AlignmentToleranceMultiplier,
        float DirectionContributionMultiplier
        ) {
            this.CorridorWidthMultiplier = CorridorWidthMultiplier;
            this.MinimumSimilarityOffset = MinimumSimilarityOffset;
            this.CoverageRequirementMultiplier = CoverageRequirementMultiplier;
            this.AlignmentToleranceMultiplier = AlignmentToleranceMultiplier;
            this.DirectionContributionMultiplier = DirectionContributionMultiplier;
        }

        public static SignatureRuleModifiers None => new(
            CorridorWidthMultiplier: 1f,
            MinimumSimilarityOffset: 0f,
            CoverageRequirementMultiplier: 1f,
            AlignmentToleranceMultiplier: 1f,
            DirectionContributionMultiplier: 1f
        );
    }
}
