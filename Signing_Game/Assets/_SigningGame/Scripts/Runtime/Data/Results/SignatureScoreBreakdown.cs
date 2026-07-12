namespace Data.Results {
    public record SignatureScoreBreakdown(
        float CorridorFit,
        float Coverage,
        float Direction,
        float StrokeStructure,
        float Total
    );
}