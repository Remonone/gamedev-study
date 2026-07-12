namespace Data.Rules {
    public record SignatureScoreWeights(
        float CorridorFit,
        float Coverage,
        float Direction,
        float StrokeStructure
    );
}