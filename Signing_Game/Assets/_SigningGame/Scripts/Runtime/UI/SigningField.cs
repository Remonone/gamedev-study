using R3;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UI {
    public enum SignatureInputEventType {
        StrokeStarted,
        PointAdded,
        StrokeEnded,
        Cleared
    }

    public readonly struct SignatureInputEvent {
        public SignatureInputEventType Type { get; }
        public Vector2 NormalizedPosition { get; }
        public float Timestamp { get; }

        public SignatureInputEvent(SignatureInputEventType type, Vector2 normalizedPosition, float timestamp) {
            Type = type;
            NormalizedPosition = normalizedPosition;
            Timestamp = timestamp;
        }
    }

    public class SigningField : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler {

        [SerializeField] private SignatureGraphic _signatureGraphic;

        [SerializeField] private float _minimumPointDistance = 2f;

        private readonly Subject<SignatureInputEvent> _inputEvents = new();

        public Observable<SignatureInputEvent> OnInput => _inputEvents;

        private RectTransform _drawingRect;

        private bool _isDrawing;
        private int _activePointerId;
        private Vector2 _lastNormalizedPosition;

        private void Awake() {
            _drawingRect = GetComponent<RectTransform>();

            _signatureGraphic.raycastTarget = false;
        }

        public void OnPointerDown(PointerEventData e) {
            if (_isDrawing) return;
            if (e.button != PointerEventData.InputButton.Left) return;

            if (!TryGetLocalPosition(e, out Vector2 localPos)) return;
            _isDrawing = true;
            _activePointerId = e.pointerId;
            _signatureGraphic.BeginStroke(localPos);
            _lastNormalizedPosition = Normalize(localPos);

            _inputEvents.OnNext(new SignatureInputEvent(
                SignatureInputEventType.StrokeStarted,
                _lastNormalizedPosition,
                Time.unscaledTime));
        }

        public void OnDrag(PointerEventData eventData) {
            if (!_isDrawing) return;

            if (eventData.pointerId != _activePointerId) return;

            if (!TryGetLocalPosition(eventData, out var position)) return;

            bool pointWasAdded = _signatureGraphic.TryAddPoint(position, _minimumPointDistance);
            if (!pointWasAdded) return;

            _lastNormalizedPosition = Normalize(position);

            _inputEvents.OnNext(new SignatureInputEvent(
                SignatureInputEventType.PointAdded,
                _lastNormalizedPosition,
                Time.unscaledTime));
        }

        public void OnPointerUp(PointerEventData e) {
            if (!_isDrawing) return;

            if (e.pointerId != _activePointerId) return;

            if (TryGetLocalPosition(e, out Vector2 localPos)) {
                _signatureGraphic.TryAddPoint(localPos, _minimumPointDistance);
                _lastNormalizedPosition = Normalize(localPos);
            }

            EndActiveStroke();
        }

        [ContextMenu("Clear canvas")]
        public void Clear() {
            CancelActiveStroke();
            _signatureGraphic.Clear();

            _inputEvents.OnNext(new SignatureInputEvent(
                SignatureInputEventType.Cleared,
                default,
                Time.unscaledTime));
        }

        private Vector2 Normalize(Vector2 position) {
            Rect rect = _drawingRect.rect;

            return new Vector2(
                Mathf.InverseLerp(rect.xMin, rect.xMax, position.x),
                Mathf.InverseLerp(rect.yMin, rect.yMax, position.y));
        }

        private bool TryGetLocalPosition(PointerEventData pointerEventData, out Vector2 position) {
            bool wasConverted = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _drawingRect,
                pointerEventData.position,
                pointerEventData.pressEventCamera,
                out position);

            if (!wasConverted) return false;

            Rect rect = _drawingRect.rect;

            position.x = Mathf.Clamp(position.x, rect.xMin, rect.xMax);
            position.y = Mathf.Clamp(position.y, rect.yMin, rect.yMax);
            return true;
        }

        private void OnDisable() {
            EndActiveStroke();
        }

        private void OnDestroy() {
            _inputEvents.Dispose();
        }

        private void EndActiveStroke() {
            if (!_isDrawing) return;

            _signatureGraphic.EndStroke();

            _isDrawing = false;
            _activePointerId = default;

            _inputEvents.OnNext(new SignatureInputEvent(
                SignatureInputEventType.StrokeEnded,
                _lastNormalizedPosition,
                Time.unscaledTime));
        }

        private void CancelActiveStroke() {
            if (!_isDrawing) return;

            _signatureGraphic.EndStroke();
            _isDrawing = false;
            _activePointerId = default;
        }
    }
}
