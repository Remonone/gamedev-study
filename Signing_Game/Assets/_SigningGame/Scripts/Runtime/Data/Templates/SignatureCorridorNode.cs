using UnityEngine;

namespace Data.Templates {
    public sealed class SignatureCorridorNode {
        public Vector2 Position { get; }
        public float Radius { get; }
        public float Importance { get; }
        public float PathProgress { get; }
        public Vector2 Direction { get; }

        public SignatureCorridorNode(Vector2 position, float radius, float importance, float pathProgress,
            Vector2 direction) {
            Position = position;
            Radius = radius;
            Importance = importance;
            PathProgress = pathProgress;
            Direction = direction;
        }
    }
}
