
using UnityEngine;

namespace Data.Processed {
    public record ProcessedSignaturePoint(
        Vector2 Position,
        Vector2 Direction,
        float PathProgress
    );
}