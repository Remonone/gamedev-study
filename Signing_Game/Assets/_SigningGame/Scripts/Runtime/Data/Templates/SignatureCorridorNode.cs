using System.Numerics;

namespace Data.Templates {
    public record SignatureCorridorNode(
        Vector2 Position,
        float Radius,
        float Importance,
        float PathProgress
    );
}