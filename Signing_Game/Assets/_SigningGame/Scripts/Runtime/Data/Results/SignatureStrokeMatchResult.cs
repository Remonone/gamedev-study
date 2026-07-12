namespace Data.Results {
    public record SignatureStrokeMatchResult(
        int InputStrokeIndex,
        string TemplateStrokeId,
        float CorridorFit,
        float Coverage,
        float Direction,
        float Similarity
    );
}