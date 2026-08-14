using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UI {
    [RequireComponent(typeof(CanvasRenderer))]
    public class SignatureGraphic : MaskableGraphic {
        [SerializeField, Min(0.5f)] private float _thickness = 3f;
        [SerializeField, Range(3, 16)] private int _roundingSegments = 8;

        private readonly List<List<Vector2>> _strokes = new();

        private List<Vector2> _activeStroke;

        public void BeginStroke(Vector2 localPos) {
            _activeStroke = new List<Vector2>(64) {
                localPos
            };
            
            _strokes.Add(_activeStroke);
            SetVerticesDirty();
        }

        public bool TryAddPoint(Vector2 localPos, float minimumDistance) {
            if(_activeStroke == null) return false;

            Vector2 prevPosition = _activeStroke[^1];

            if ((localPos - prevPosition).sqrMagnitude < minimumDistance * minimumDistance) return false;
            
            _activeStroke.Add(localPos);
            SetVerticesDirty();
            return true;
        }

        public void EndStroke() {
            _activeStroke = null;
        }

        public void SetStrokes(IReadOnlyList<IReadOnlyList<Vector2>> strokes) {
            _activeStroke = null;
            _strokes.Clear();

            if (strokes != null) {
                for (int strokeIndex = 0; strokeIndex < strokes.Count; strokeIndex++) {
                    IReadOnlyList<Vector2> sourceStroke = strokes[strokeIndex];
                    if (sourceStroke == null || sourceStroke.Count == 0) continue;

                    var stroke = new List<Vector2>(sourceStroke.Count);
                    for (int pointIndex = 0; pointIndex < sourceStroke.Count; pointIndex++) {
                        stroke.Add(sourceStroke[pointIndex]);
                    }
                    _strokes.Add(stroke);
                }
            }

            SetVerticesDirty();
        }

        public void Clear() {
            _activeStroke = null;
            _strokes.Clear();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh) {
            vh.Clear();
            
            float radius = _thickness / 2f;
            Color32 vertexColor = color;
            
            foreach (var stroke in _strokes) {
                if (stroke.Count == 0) continue;
                
                AddDisc(vh, stroke[0], radius, _roundingSegments, vertexColor);

                for (int i = 1; i < stroke.Count; i++) {
                    Vector2 previous = stroke[i - 1];
                    Vector2 current = stroke[i];
                    
                    AddSegment(vh, previous, current, radius, vertexColor);
                    
                    AddDisc(vh, current, radius, _roundingSegments, vertexColor);
                }
            }
        }

        private void AddDisc(VertexHelper vertexHelper, Vector2 point, float radius, int segmentsCount, Color32 color) {
            int centerIndex = vertexHelper.currentVertCount;
            
            vertexHelper.AddVert(point, color, Vector2.zero);

            for (int i = 0; i <= segmentsCount; i++) {
                float angle = i / (float)segmentsCount * Mathf.PI * 2f;
                
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                
                vertexHelper.AddVert(point + offset, color, Vector2.zero);
            }

            for (int i = 0; i < segmentsCount; i++) {
                vertexHelper.AddTriangle(centerIndex, centerIndex + i + 1, centerIndex + i + 2);
            }
        }

        private void AddSegment(VertexHelper vertexHelper, Vector2 start, Vector2 end, float radius,
            Color32 vertexColor) {
            Vector2 direction = end - start;
            float lengthSquared = direction.sqrMagnitude;
            
            if (lengthSquared < Mathf.Epsilon)
                return;
            
            float inverseLength = 1f / Mathf.Sqrt(lengthSquared);
            
            Vector2 normal = new Vector2(-direction.y * inverseLength, direction.x * inverseLength) * radius;
            
            int startIndex = vertexHelper.currentVertCount;
            
            vertexHelper.AddVert(start + normal, vertexColor, Vector2.zero);
            vertexHelper.AddVert(start - normal, vertexColor, Vector2.zero);
            vertexHelper.AddVert(end - normal, vertexColor, Vector2.zero);
            vertexHelper.AddVert(end + normal, vertexColor, Vector2.zero);
            
            vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vertexHelper.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
        }
        
#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif
    }
}
