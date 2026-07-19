namespace Data.Rules {
    public record SignatureProcessingRules(
        float MinimumInputPointDistance,
        int MinimumUsablePointCountPerStroke,
        float MinimumStrokeLength,
        int ResampledPointCountPerStroke,
        int SmoothingPasses,
        int MaximumInputPointCount
    );
}
