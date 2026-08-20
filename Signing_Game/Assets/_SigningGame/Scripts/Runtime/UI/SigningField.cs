using System;
using System.Collections;
using System.Collections.Generic;
using R3;
using Services;
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
        [SerializeField] private SignatureGraphic _guideGraphic;

        [SerializeField] private float _minimumPointDistance = 2f;

        private readonly Subject<SignatureInputEvent> _inputEvents = new();

        public Observable<SignatureInputEvent> OnInput => _inputEvents;

        private RectTransform _drawingRect;
        private RectTransform _signatureRect;

        private bool _isDrawing;
        private int _activePointerId;
        private Vector2 _lastNormalizedPosition;
        private float _lastPointerTimestamp;

        private SignatureGuidanceSnapshot _guidanceSnapshot;
        private IReadOnlyList<IReadOnlyList<Vector2>> _mappedGuidanceStrokes;
        private int _completedGuidanceStrokes;
        private int _animatingStrokeIndex = -1;
        private Coroutine _revealCoroutine;

        private void Awake() {
            _drawingRect = GetComponent<RectTransform>();
            _signatureRect = _signatureGraphic.rectTransform;

            _signatureGraphic.raycastTarget = false;
            if (_guideGraphic != null) _guideGraphic.raycastTarget = false;
        }

        public bool ConfigureGuidance(SignatureGuidanceSnapshot snapshot) {
            CancelRevealAnimation(false);
            _guidanceSnapshot = null;
            _mappedGuidanceStrokes = null;
            _completedGuidanceStrokes = 0;

            if (snapshot == null || _guideGraphic == null) {
                if (_guideGraphic != null) {
                    _guideGraphic.Clear();
                    _guideGraphic.gameObject.SetActive(false);
                }
                _guidanceSnapshot = snapshot;
                return true;
            }

            if (!TryMapGuidanceStrokes(snapshot, out IReadOnlyList<IReadOnlyList<Vector2>> mapped)) {
                HideGuidance();
                return false;
            }

            _guidanceSnapshot = snapshot;
            _mappedGuidanceStrokes = mapped;
            ApplyGuidanceBaseline();
            return true;
        }

        public void HideGuidance() {
            CancelRevealAnimation(false);
            _guidanceSnapshot = null;
            _mappedGuidanceStrokes = null;
            _completedGuidanceStrokes = 0;
            if (_guideGraphic == null) return;

            _guideGraphic.Clear();
            _guideGraphic.gameObject.SetActive(false);
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
            CancelRevealAnimation(false);
            _signatureGraphic.Clear();
            _completedGuidanceStrokes = 0;
            ApplyGuidanceBaseline();

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
            CancelRevealAnimation(true);
            EndActiveStroke(_lastPointerTimestamp);
        }

        private void OnDestroy() {
            CancelRevealAnimation(false);
            _inputEvents.Dispose();
        }

        private void EndActiveStroke(float timestamp) {
            if (!_isDrawing) return;

            _signatureGraphic.EndStroke();

            _isDrawing = false;
            _activePointerId = default;

            AdvanceGuidanceAfterStroke();

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

        private void ApplyGuidanceBaseline() {
            if (_guideGraphic == null || _guidanceSnapshot == null || _mappedGuidanceStrokes == null) return;

            if (_guidanceSnapshot.Phase == SignatureGuidancePhaseKind.Hidden ||
                _mappedGuidanceStrokes.Count == 0) {
                _guideGraphic.Clear();
                _guideGraphic.gameObject.SetActive(false);
                return;
            }

            _guideGraphic.gameObject.SetActive(true);
            _guideGraphic.SetStrokes(_mappedGuidanceStrokes);
            var alphas = new float[_mappedGuidanceStrokes.Count];
            if (_guidanceSnapshot.Phase == SignatureGuidancePhaseKind.Full) {
                for (int index = 0; index < alphas.Length; index++) alphas[index] = _guidanceSnapshot.Alpha;
            }
            else {
                alphas[0] = SignatureGuidancePhase.MaximumAlpha;
            }
            _guideGraphic.SetStrokeAlphas(alphas);
        }

        private void AdvanceGuidanceAfterStroke() {
            if (_guideGraphic == null || _guidanceSnapshot == null ||
                _guidanceSnapshot.Phase != SignatureGuidancePhaseKind.Progressive ||
                _mappedGuidanceStrokes == null || _mappedGuidanceStrokes.Count == 0) return;

            int completed = Mathf.Clamp(_completedGuidanceStrokes + 1, 0, _mappedGuidanceStrokes.Count);
            _completedGuidanceStrokes = completed;
            if (completed >= _mappedGuidanceStrokes.Count) return;

            int nextStrokeIndex = completed;
            _guideGraphic.SetStrokeAlpha(nextStrokeIndex, 0f);
            CancelRevealAnimation(false);
            _animatingStrokeIndex = nextStrokeIndex;
            if (!isActiveAndEnabled) {
                _guideGraphic.SetStrokeAlpha(nextStrokeIndex, SignatureGuidancePhase.MaximumAlpha);
                _animatingStrokeIndex = -1;
                return;
            }

            _revealCoroutine = StartCoroutine(RevealStroke(nextStrokeIndex));
        }

        private IEnumerator RevealStroke(int strokeIndex) {
            float elapsed = 0f;
            while (elapsed < SignatureGuidancePhase.RevealDurationSeconds) {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / SignatureGuidancePhase.RevealDurationSeconds);
                _guideGraphic.SetStrokeAlpha(strokeIndex,
                    SignatureGuidancePhase.MaximumAlpha * progress);
                yield return null;
            }

            _guideGraphic.SetStrokeAlpha(strokeIndex, SignatureGuidancePhase.MaximumAlpha);
            _revealCoroutine = null;
            _animatingStrokeIndex = -1;
        }

        private void CancelRevealAnimation(bool complete) {
            if (_revealCoroutine != null) {
                StopCoroutine(_revealCoroutine);
                _revealCoroutine = null;
            }

            if (complete && _animatingStrokeIndex >= 0 && _guideGraphic != null &&
                _animatingStrokeIndex < _guideGraphic.StrokeCount) {
                _guideGraphic.SetStrokeAlpha(_animatingStrokeIndex, SignatureGuidancePhase.MaximumAlpha);
            }
            _animatingStrokeIndex = -1;
        }

        private bool TryMapGuidanceStrokes(SignatureGuidanceSnapshot snapshot,
            out IReadOnlyList<IReadOnlyList<Vector2>> mapped) {
            mapped = null;
            Rect rect = _guideGraphic.rectTransform.rect;
            if (!IsFinite(rect.width) || !IsFinite(rect.height) || rect.width <= 0f || rect.height <= 0f)
                return false;

            var result = new List<IReadOnlyList<Vector2>>(snapshot.Strokes.Count);
            for (int strokeIndex = 0; strokeIndex < snapshot.Strokes.Count; strokeIndex++) {
                IReadOnlyList<Vector2> sourceStroke = snapshot.Strokes[strokeIndex];
                if (sourceStroke == null || sourceStroke.Count == 0) return false;

                var points = new Vector2[sourceStroke.Count];
                for (int pointIndex = 0; pointIndex < sourceStroke.Count; pointIndex++) {
                    Vector2 normalized = sourceStroke[pointIndex];
                    if (!IsFinite(normalized.x) || !IsFinite(normalized.y) || normalized.x < 0f ||
                        normalized.x > 1f || normalized.y < 0f || normalized.y > 1f) return false;
                    points[pointIndex] = MapNormalizedPosition(rect, normalized);
                }
                result.Add(Array.AsReadOnly(points));
            }

            mapped = Array.AsReadOnly(result.ToArray());
            return true;
        }

        public static Vector2 MapNormalizedPosition(Rect rect, Vector2 normalized) {
            return new Vector2(rect.xMin + normalized.x * rect.width,
                rect.yMin + normalized.y * rect.height);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
