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
        private RectTransform _signatureRect;

        private bool _isDrawing;
        private int _activePointerId;
        private Vector2 _lastNormalizedPosition;
        private float _lastPointerTimestamp;

        private void Awake() {
            _drawingRect = GetComponent<RectTransform>();
            _signatureRect = _signatureGraphic.rectTransform;

            _signatureGraphic.raycastTarget = false;
        }

        public void OnPointerDown(PointerEventData e) {
            if (_isDrawing) return;
            if (e.button != PointerEventData.InputButton.Left) return;

            if (!TryGetLocalPosition(e, out Vector2 localPos)) return;
            _isDrawing = true;
            _activePointerId = e.pointerId;
            _signatureGraphic.BeginStroke(ToSignatureLocalPosition(localPos));
            _lastNormalizedPosition = Normalize(localPos);
            _lastPointerTimestamp = Time.unscaledTime;

            _inputEvents.OnNext(new SignatureInputEvent(
                SignatureInputEventType.StrokeStarted,
                _lastNormalizedPosition,
                _lastPointerTimestamp));
        }

        public void OnDrag(PointerEventData eventData) {
            if (!_isDrawing) return;

            if (eventData.pointerId != _activePointerId) return;

            if (!TryGetLocalPosition(eventData, out var position)) return;

            _lastNormalizedPosition = Normalize(position);
            _lastPointerTimestamp = Time.unscaledTime;

            bool pointWasAdded = _signatureGraphic.TryAddPoint(
                ToSignatureLocalPosition(position),
                _minimumPointDistance);
            if (!pointWasAdded) return;

            _inputEvents.OnNext(new SignatureInputEvent(
                SignatureInputEventType.PointAdded,
                _lastNormalizedPosition,
                _lastPointerTimestamp));
        }

        public void OnPointerUp(PointerEventData e) {
            if (!_isDrawing) return;

            if (e.pointerId != _activePointerId) return;

            if (TryGetLocalPosition(e, out Vector2 localPos)) {
                _signatureGraphic.TryAddPoint(ToSignatureLocalPosition(localPos), _minimumPointDistance);
                _lastNormalizedPosition = Normalize(localPos);
                _lastPointerTimestamp = Time.unscaledTime;
            }

            FinishActiveStrokeForCollection(_lastPointerTimestamp);
        }

        public void FinishActiveStrokeForCollection(float endTime) {
            if (!_isDrawing) return;

            EndActiveStroke(_lastPointerTimestamp);
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

        private Vector2 ToSignatureLocalPosition(Vector2 drawingLocalPosition) {
            if (_signatureRect == _drawingRect) return drawingLocalPosition;

            Vector3 worldPosition = _drawingRect.TransformPoint(drawingLocalPosition);
            return _signatureRect.InverseTransformPoint(worldPosition);
        }

        private void OnDisable() {
            EndActiveStroke(_lastPointerTimestamp);
        }

        private void OnDestroy() {
            _inputEvents.Dispose();
        }

        private void EndActiveStroke(float timestamp) {
            if (!_isDrawing) return;

            _signatureGraphic.EndStroke();

            _isDrawing = false;
            _activePointerId = default;

            _inputEvents.OnNext(new SignatureInputEvent(
                SignatureInputEventType.StrokeEnded,
                _lastNormalizedPosition,
                timestamp));
        }

        private void CancelActiveStroke() {
            if (!_isDrawing) return;

            _signatureGraphic.EndStroke();
            _isDrawing = false;
            _activePointerId = default;
        }
    }
}
