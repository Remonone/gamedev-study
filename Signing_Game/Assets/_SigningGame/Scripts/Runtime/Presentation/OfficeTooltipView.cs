using TMPro;
using UnityEngine;

namespace Presentation {
    public sealed class OfficeTooltipView : MonoBehaviour {
        [SerializeField] private RectTransform _panel;
        [SerializeField] private TextMeshProUGUI _text;
        [SerializeField] private Vector2 _pointerOffset = new(18f, -18f);

        public void Show(string text, Vector2 screenPosition, Camera eventCamera) {
            if (_panel == null || _text == null || _panel.parent is not RectTransform parent) return;
            _text.text = text;
            _panel.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent,
                    screenPosition,
                    eventCamera,
                    out Vector2 localPoint)) {
                return;
            }

            Vector2 desired = localPoint + _pointerOffset;
            Rect parentRect = parent.rect;
            Rect panelRect = _panel.rect;
            float halfWidth = panelRect.width * 0.5f;
            float halfHeight = panelRect.height * 0.5f;
            desired.x = Mathf.Clamp(desired.x, parentRect.xMin + halfWidth, parentRect.xMax - halfWidth);
            desired.y = Mathf.Clamp(desired.y, parentRect.yMin + halfHeight, parentRect.yMax - halfHeight);
            _panel.anchoredPosition = desired;
            _panel.SetAsLastSibling();
        }

        public void Hide() {
            if (_panel != null) _panel.gameObject.SetActive(false);
        }

        private void Awake() {
            Hide();
        }

        private void OnDisable() {
            Hide();
        }
    }
}
