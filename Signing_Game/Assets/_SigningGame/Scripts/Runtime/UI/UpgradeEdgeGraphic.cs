using System.Collections.Generic;
using Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace UI {
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UpgradeEdgeGraphic : MaskableGraphic {
        [SerializeField, Min(0.5f)] private float _thickness = 4f;
        [SerializeField, Range(4, 64)] private int _segments = 20;
        [SerializeField, Min(0f)] private float _curvature = 80f;

        private readonly List<UpgradeEdgePresentationModel> _edges = new();

        protected override void Awake() {
            base.Awake();
            raycastTarget = false;
        }

        public void SetEdges(IReadOnlyList<UpgradeEdgePresentationModel> edges) {
            if (Matches(edges)) return;

            _edges.Clear();
            if (edges != null) {
                for (int index = 0; index < edges.Count; index++) _edges.Add(edges[index]);
            }

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper) {
            vertexHelper.Clear();
            float radius = _thickness * 0.5f;
            Color32 vertexColor = color;

            for (int edgeIndex = 0; edgeIndex < _edges.Count; edgeIndex++) {
                UpgradeEdgePresentationModel edge = _edges[edgeIndex];
                Vector2 delta = edge.End - edge.Start;
                float length = delta.magnitude;
                if (length <= Mathf.Epsilon) continue;

                Vector2 perpendicular = new(-delta.y / length, delta.x / length);
                float bend = Mathf.Min(_curvature, length * 0.2f);
                Vector2 firstControl = edge.Start + delta / 3f + perpendicular * bend;
                Vector2 secondControl = edge.Start + delta * (2f / 3f) + perpendicular * bend;
                Vector2 previous = edge.Start;

                for (int segment = 1; segment <= _segments; segment++) {
                    float t = segment / (float)_segments;
                    Vector2 current = EvaluateCubic(
                        edge.Start, firstControl, secondControl, edge.End, t);
                    AddSegment(vertexHelper, previous, current, radius, vertexColor);
                    previous = current;
                }
            }
        }

        private bool Matches(IReadOnlyList<UpgradeEdgePresentationModel> edges) {
            int count = edges?.Count ?? 0;
            if (_edges.Count != count) return false;

            for (int index = 0; index < count; index++) {
                UpgradeEdgePresentationModel current = _edges[index];
                UpgradeEdgePresentationModel next = edges[index];
                if (current.ParentId != next.ParentId || current.ChildId != next.ChildId ||
                    current.Start != next.Start || current.End != next.End) return false;
            }

            return true;
        }

        private static Vector2 EvaluateCubic(Vector2 start, Vector2 firstControl, Vector2 secondControl,
            Vector2 end, float t) {
            float inverse = 1f - t;
            float inverseSquared = inverse * inverse;
            float tSquared = t * t;
            return inverseSquared * inverse * start +
                   3f * inverseSquared * t * firstControl +
                   3f * inverse * tSquared * secondControl +
                   tSquared * t * end;
        }

        private static void AddSegment(VertexHelper vertexHelper, Vector2 start, Vector2 end, float radius,
            Color32 vertexColor) {
            Vector2 direction = end - start;
            float lengthSquared = direction.sqrMagnitude;
            if (lengthSquared <= Mathf.Epsilon) return;

            float inverseLength = 1f / Mathf.Sqrt(lengthSquared);
            Vector2 normal = new(-direction.y * inverseLength * radius, direction.x * inverseLength * radius);
            int startIndex = vertexHelper.currentVertCount;
            vertexHelper.AddVert(start + normal, vertexColor, Vector2.zero);
            vertexHelper.AddVert(start - normal, vertexColor, Vector2.zero);
            vertexHelper.AddVert(end - normal, vertexColor, Vector2.zero);
            vertexHelper.AddVert(end + normal, vertexColor, Vector2.zero);
            vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vertexHelper.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
        }

#if UNITY_EDITOR
        protected override void OnValidate() {
            base.OnValidate();
            raycastTarget = false;
            SetVerticesDirty();
        }
#endif
    }
}
