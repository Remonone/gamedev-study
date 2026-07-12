namespace Data.Rules {
    public record SignatureAlignmentRules(
        float MaximumTranslation,
        float MinimumScale,
        float MaximumScale,
        float MaximumRotationDegrees
    );
}