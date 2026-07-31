using UnityEngine;
using UnityEngine.EventSystems;

namespace UI {
    [RequireComponent(typeof(RectTransform))]
    public sealed class UpgradeTreePanView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler {
        [SerializeField] private RectTransform _content;
        [SerializeField] private RectTransform _dragHandle;

        private RectTransform _viewport;
        private bool _isDragging;
        private int _pointerId;
        private Vector2 _pointerStart;
        private Vector2 _contentStart;

        private void Awake() {
            _viewport = (RectTransform)transform;
            _dragHandle ??= _viewport;
        }

        public void OnBeginDrag(PointerEventData eventData) {
            if (_isDragging || _content == null || eventData.button != PointerEventData.InputButton.Left) return;
            if (eventData.pointerPressRaycast.gameObject == null ||
                eventData.pointerPressRaycast.gameObject.transform != _dragHandle) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _viewport, eventData.position, eventData.pressEventCamera, out _pointerStart)) return;

            _pointerId = eventData.pointerId;
            _contentStart = _content.anchoredPosition;
            _isDragging = true;
        }

        public void OnDrag(PointerEventData eventData) {
            if (!_isDragging || eventData.pointerId != _pointerId) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _viewport, eventData.position, eventData.pressEventCamera, out Vector2 pointer)) return;

            _content.anchoredPosition = _contentStart + pointer - _pointerStart;
        }

        public void OnEndDrag(PointerEventData eventData) {
            if (_isDragging && eventData.pointerId == _pointerId) ResetDrag();
        }

        private void OnDisable() {
            ResetDrag();
        }

        private void ResetDrag() {
            _isDragging = false;
            _pointerId = default;
        }
    }
}
