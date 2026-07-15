using UnityEngine;
using UnityEngine.EventSystems;

namespace UI {
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class DocumentDragView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler {
        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private RectTransform _parentRectTransform;
        private bool _isDragging;
        private bool _previousBlocksRaycasts;
        private int _activePointerId;
        private Vector2 _pointerStartPosition;
        private Vector2 _anchoredStartPosition;

        private void Awake() {
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        public void OnBeginDrag(PointerEventData eventData) {
            if (_isDragging || eventData.button != PointerEventData.InputButton.Left) return;

            _parentRectTransform = transform.parent as RectTransform;
            if (_parentRectTransform == null) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _parentRectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out _pointerStartPosition)) {
                _parentRectTransform = null;
                return;
            }

            _activePointerId = eventData.pointerId;
            _anchoredStartPosition = _rectTransform.anchoredPosition;
            _previousBlocksRaycasts = _canvasGroup.blocksRaycasts;
            _canvasGroup.blocksRaycasts = false;
            _isDragging = true;
        }

        public void OnDrag(PointerEventData eventData) {
            if (!_isDragging || eventData.pointerId != _activePointerId) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _parentRectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 pointerPosition)) return;

            _rectTransform.anchoredPosition =
                _anchoredStartPosition + pointerPosition - _pointerStartPosition;
        }

        public void OnEndDrag(PointerEventData eventData) {
            if (!_isDragging || eventData.pointerId != _activePointerId) return;

            ResetDrag();
        }

        private void OnDisable() {
            ResetDrag();
        }

        private void OnDestroy() {
            ResetDrag();
        }

        private void ResetDrag() {
            if (!_isDragging) return;

            _canvasGroup.blocksRaycasts = _previousBlocksRaycasts;
            _isDragging = false;
            _activePointerId = default;
            _parentRectTransform = null;
        }
    }
}
