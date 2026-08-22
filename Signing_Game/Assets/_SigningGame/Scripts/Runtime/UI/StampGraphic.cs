using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UI {
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StampGraphic : MaskableGraphic {
        private readonly List<Rect> _stamps = new();

        public int StampCount => _stamps.Count;

        public bool TryAddStamp(Rect localRect) {
            Rect documentRect = rectTransform.rect;
            float xMin = Mathf.Max(localRect.xMin, documentRect.xMin);
            float yMin = Mathf.Max(localRect.yMin, documentRect.yMin);
            float xMax = Mathf.Min(localRect.xMax, documentRect.xMax);
            float yMax = Mathf.Min(localRect.yMax, documentRect.yMax);
            if (xMax <= xMin || yMax <= yMin) return false;

            _stamps.Add(Rect.MinMaxRect(xMin, yMin, xMax, yMax));
            SetVerticesDirty();
            return true;
        }

        public void Clear() {
            if (_stamps.Count == 0) return;
            _stamps.Clear();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper) {
            vertexHelper.Clear();
            Color32 stampColor = color;

            for (int index = 0; index < _stamps.Count; index++) {
                Rect stamp = _stamps[index];
                int startIndex = vertexHelper.currentVertCount;
                vertexHelper.AddVert(new Vector3(stamp.xMin, stamp.yMin), stampColor, Vector2.zero);
                vertexHelper.AddVert(new Vector3(stamp.xMin, stamp.yMax), stampColor, Vector2.zero);
                vertexHelper.AddVert(new Vector3(stamp.xMax, stamp.yMax), stampColor, Vector2.zero);
                vertexHelper.AddVert(new Vector3(stamp.xMax, stamp.yMin), stampColor, Vector2.zero);
                vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
                vertexHelper.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
            }
        }
    }
}
