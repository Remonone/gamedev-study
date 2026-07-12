using System.Numerics;

namespace Data.Processed {
    public record ProcessedSignaturePoint(
        Vector2 Position,
        Vector2 Direction,
        float PathProgress
    );
}