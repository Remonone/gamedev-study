using UnityEngine;
using UnityEngine.UI;

namespace Presentation {
    public sealed class BillRequirementFlowLayoutGroup : LayoutGroup {
        [SerializeField] private Vector2 _cellSize = new(120f, 28f);
        [SerializeField] private Vector2 _spacing = new(8f, 6f);

        public override void CalculateLayoutInputHorizontal() {
            base.CalculateLayoutInputHorizontal();
            SetLayoutInputForAxis(padding.horizontal + _cellSize.x, -1f, -1f, 0);
        }

        public override void CalculateLayoutInputVertical() {
            float width = Mathf.Max(_cellSize.x, rectTransform.rect.width - padding.horizontal);
            int columns = Mathf.Max(1, Mathf.FloorToInt((width + _spacing.x) / (_cellSize.x + _spacing.x)));
            int rows = Mathf.CeilToInt((float)rectChildren.Count / columns);
            float height = padding.vertical + rows * _cellSize.y + Mathf.Max(0, rows - 1) * _spacing.y;
            SetLayoutInputForAxis(height, height, height, 1);
        }

        public override void SetLayoutHorizontal() => Arrange();
        public override void SetLayoutVertical() => Arrange();

        private void Arrange() {
            float width = Mathf.Max(_cellSize.x, rectTransform.rect.width - padding.horizontal);
            int columns = Mathf.Max(1, Mathf.FloorToInt((width + _spacing.x) / (_cellSize.x + _spacing.x)));
            for (int index = 0; index < rectChildren.Count; index++) {
                int row = index / columns;
                int column = index % columns;
                SetChildAlongAxis(rectChildren[index], 0, padding.left + column * (_cellSize.x + _spacing.x), _cellSize.x);
                SetChildAlongAxis(rectChildren[index], 1, padding.top + row * (_cellSize.y + _spacing.y), _cellSize.y);
            }
        }
    }
}
