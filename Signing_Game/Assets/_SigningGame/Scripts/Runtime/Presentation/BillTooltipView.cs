using TMPro;
using UnityEngine;

namespace Presentation {
    public sealed class BillTooltipView : MonoBehaviour {
        [SerializeField] private RectTransform _panel;
        [SerializeField] private TextMeshProUGUI _text;
        [SerializeField] private Vector2 _pointerOffset = new(18f, -18f);

        public void Show(string text, Vector2 screenPosition, Camera eventCamera) {
            if (string.IsNullOrWhiteSpace(text) || _panel == null || _text == null ||
                _panel.parent is not RectTransform parent) return;
            _text.text = text;
            _panel.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, screenPosition, eventCamera, out Vector2 localPoint)) return;
            Rect parentRect = parent.rect;
            Rect panelRect = _panel.rect;
            Vector2 desired = localPoint + _pointerOffset;
            desired.x = Mathf.Clamp(
                desired.x,
                parentRect.xMin + panelRect.width * 0.5f,
                parentRect.xMax - panelRect.width * 0.5f);
            desired.y = Mathf.Clamp(
                desired.y,
                parentRect.yMin + panelRect.height * 0.5f,
                parentRect.yMax - panelRect.height * 0.5f);
            _panel.anchoredPosition = desired;
            _panel.SetAsLastSibling();
        }

        public void Hide() {
            if (_panel != null) _panel.gameObject.SetActive(false);
        }

        private void Awake() => Hide();
        private void OnDisable() => Hide();
    }
}
