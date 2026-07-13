using Presentation;
using R3;
using UnityEngine;
using UnityEngine.EventSystems;

public enum SignatureInputEventType {
    StrokeStarted,
    PointAdded,
    StrokeEnded
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
        _lastNormalizedPosition = localPos;
        
        _inputEvents.OnNext(new SignatureInputEvent(
                SignatureInputEventType.StrokeStarted,
                _lastNormalizedPosition,
                Time.unscaledTime
            ));
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
                Time.unscaledTime
            ));
        
    }

    public void OnPointerUp(PointerEventData e) {
        if (!_isDrawing) return;

        if (e.pointerId != _activePointerId) return;

        if (TryGetLocalPosition(e, out Vector2 localPos)) {
            bool pointWasAdded = _signatureGraphic.TryAddPoint(localPos, _minimumPointDistance);

            if (pointWasAdded) {
                _lastNormalizedPosition = Normalize(localPos);
                
                _inputEvents.OnNext(new SignatureInputEvent(
                        SignatureInputEventType.PointAdded,
                        _lastNormalizedPosition,
                        Time.unscaledTime
                    ));
            }
        }
        
        _signatureGraphic.EndStroke();
        
        _inputEvents.OnNext(new SignatureInputEvent(
            SignatureInputEventType.StrokeEnded,
            _lastNormalizedPosition,
            Time.unscaledTime));
        
        _isDrawing = false;

        _activePointerId = default;
    }
    
    [ContextMenu("Clear canvas")]
    public void Clear() {
        _isDrawing = false;
        _activePointerId = default;
        
        _signatureGraphic.Clear();
    }

    private Vector2 Normalize(Vector2 position) {
        Rect rect = _drawingRect.rect;

        return new Vector2(
            Mathf.InverseLerp(rect.xMin, rect.xMax, position.x),
            Mathf.InverseLerp(rect.yMin, rect.yMax, position.y));
    }
    
    
    private bool TryGetLocalPosition(PointerEventData pointerEventData, out Vector2 position) {
        bool wasConverted = RectTransformUtility.ScreenPointToLocalPointInRectangle(_drawingRect, pointerEventData.position, pointerEventData.pressEventCamera, out position);
        if (!wasConverted) return false;
        Rect rect = _drawingRect.rect;
        
        position.x = Mathf.Clamp(position.x, rect.xMin, rect.xMax);
        position.y = Mathf.Clamp(position.y, rect.yMin, rect.yMax);
        return true;
    }

    private void OnDisable() {
        if (!_isDrawing) return;
        
        _signatureGraphic.EndStroke();
        
        _isDrawing = false;
        _activePointerId = default;
    }
    
    private void OnDestroy() {
        _inputEvents.Dispose();
    }
}
