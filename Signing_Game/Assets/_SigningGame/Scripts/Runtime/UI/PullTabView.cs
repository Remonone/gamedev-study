using System;
using DG.Tweening;
using R3;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UI {
    public enum PullTabAxis {
        X,
        Y
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class PullTabView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler {
        [Header("Movement")]
        [SerializeField] private PullTabAxis _movementAxis = PullTabAxis.X;
        [SerializeField] private RectTransform _pulledObject;
        [SerializeField] private Transform _startPosition;
        [SerializeField] private Transform _stopPosition;
        [SerializeField] private Transform _disabledPosition;

        [Header("Threshold")]
        [SerializeField, Min(0f)] private float _threshold = 100f;
        [SerializeField] private bool _useOpeningThreshold = true;
        [SerializeField] private bool _useClosingThreshold = true;

        [Header("State")]
        [SerializeField] private bool _initiallyAvailable = true;
        [SerializeField] private bool _bringToFrontWhenOpen = true;

        [Header("Animation")]
        [SerializeField, Min(0f)] private float _snapDuration = 0.2f;
        [SerializeField] private Ease _snapEase = Ease.OutCubic;

        private readonly ReactiveProperty<bool> _availabilityState = new(false);
        private readonly ReactiveProperty<bool> _openState = new(false);

        private RectTransform _handle;
        private RectTransform _commonParent;
        private CanvasGroup _handleCanvasGroup;
        private CanvasGroup _pulledObjectCanvasGroup;
        private IDisposable _availabilitySubscription;
        private Tween _movementTween;

        private bool _initialized;
        private bool _isValid;
        private bool _isDragging;
        private bool _isSnapping;
        private bool _dragStartedOpen;
        private int _activePointerId;
        private Vector2 _pointerStartPosition;
        private float _handleDragStartAxis;
        private float _closedAxis;
        private float _openAxis;
        private float _disabledAxis;
        private float _pulledObjectClosedAxis;

        public bool IsAvailable => _availabilityState.Value;
        public bool IsOpen => _openState.Value;
        public ReadOnlyReactiveProperty<bool> AvailabilityState => _availabilityState;
        public ReadOnlyReactiveProperty<bool> OpenState => _openState;

        private void Awake() {
            if (TryInitialize()) return;

            enabled = false;
        }

        private void OnEnable() {
            if (!TryInitialize()) return;

            CancelMotion();
            SnapToLogicalPosition();
            RefreshInteraction();
        }

        public void OnBeginDrag(PointerEventData eventData) {
            if (!_isValid || !IsAvailable || _isDragging || _isSnapping ||
                eventData.button != PointerEventData.InputButton.Left) return;
            if (!TryGetPointerPosition(eventData, out _pointerStartPosition)) return;

            if (_bringToFrontWhenOpen) BringToFront();

            _activePointerId = eventData.pointerId;
            _handleDragStartAxis = GetAxis(_handle.localPosition);
            _dragStartedOpen = IsOpen;
            _isDragging = true;
            RefreshInteraction();
        }

        public void OnDrag(PointerEventData eventData) {
            if (!_isDragging || eventData.pointerId != _activePointerId) return;
            if (!TryGetPointerPosition(eventData, out Vector2 pointerPosition)) return;

            float pointerDelta = GetAxis(pointerPosition - _pointerStartPosition);
            float desiredAxis = Mathf.Clamp(
                _handleDragStartAxis + pointerDelta,
                Mathf.Min(_closedAxis, _openAxis),
                Mathf.Max(_closedAxis, _openAxis));
            ApplyAxisPosition(desiredAxis);
        }

        public void OnEndDrag(PointerEventData eventData) {
            if (!_isDragging || eventData.pointerId != _activePointerId) return;

            bool startedOpen = _dragStartedOpen;
            float startStateAxis = startedOpen ? _openAxis : _closedAxis;
            float distance = Mathf.Abs(GetAxis(_handle.localPosition) - startStateAxis);
            bool useThreshold = startedOpen ? _useClosingThreshold : _useOpeningThreshold;
            bool shouldOpen = ResolveOpenState(startedOpen, useThreshold, distance, _threshold);

            ResetDrag();
            SetOpen(shouldOpen);
        }

        public void SetAvailable(bool available) {
            if (!TryInitialize()) return;

            CancelMotion();
            if (IsAvailable == available) {
                SnapToLogicalPosition();
                RefreshInteraction();
                return;
            }

            _availabilityState.Value = available;
            _openState.Value = false;
            ApplyAxisPosition(available ? _closedAxis : _disabledAxis);
            RefreshInteraction();
        }

        public void SetOpen(bool open, bool immediate = false) {
            if (!TryInitialize()) return;
            if (!IsAvailable && open) return;

            CancelMotion();
            if (!IsAvailable) {
                _openState.Value = false;
                ApplyAxisPosition(_disabledAxis);
                RefreshInteraction();
                return;
            }

            _openState.Value = open;
            if (open && _bringToFrontWhenOpen) BringToFront();

            float targetAxis = open ? _openAxis : _closedAxis;
            if (immediate || _snapDuration <= 0f ||
                Mathf.Approximately(GetAxis(_handle.localPosition), targetAxis)) {
                ApplyAxisPosition(targetAxis);
                RefreshInteraction();
                return;
            }

            _isSnapping = true;
            RefreshInteraction();
            _movementTween = DOTween
                .To(() => GetAxis(_handle.localPosition), ApplyAxisPosition, targetAxis, _snapDuration)
                .SetEase(_snapEase)
                .SetTarget(this)
                .OnComplete(CompleteSnap);
        }

        /// <summary>
        /// Binds availability for this object's lifetime. Rebinding or destroying this view disposes the old subscription.
        /// </summary>
        public void BindAvailability(Observable<bool> availability) {
            if (availability == null) throw new ArgumentNullException(nameof(availability));

            UnbindAvailability();
            _availabilitySubscription = availability.Subscribe(SetAvailable);
        }

        public void UnbindAvailability() {
            _availabilitySubscription?.Dispose();
            _availabilitySubscription = null;
        }

        private void OnDisable() {
            CancelMotion();
            if (_isValid) SnapToLogicalPosition();
            DisableInteraction();
        }

        private void OnDestroy() {
            CancelMotion();
            UnbindAvailability();
            _availabilityState.Dispose();
            _openState.Dispose();
        }

        internal static bool ResolveOpenState(
            bool startedOpen,
            bool useThreshold,
            float distance,
            float threshold) {
            if (!useThreshold) return !startedOpen;

            bool reachedThreshold = Mathf.Max(0f, distance) >= Mathf.Max(0f, threshold);
            return reachedThreshold ? !startedOpen : startedOpen;
        }

        private bool TryInitialize() {
            if (_initialized) return _isValid;

            _initialized = true;
            _handle = transform as RectTransform;
            _handleCanvasGroup = GetComponent<CanvasGroup>();
            _commonParent = _handle != null ? _handle.parent as RectTransform : null;

            if (_handle == null || _handleCanvasGroup == null || _commonParent == null ||
                _pulledObject == null || _startPosition == null || _stopPosition == null ||
                _disabledPosition == null) {
                return FailInitialization("PullTabView requires a handle, a RectTransform parent, a pulled object, and all three position references.");
            }

            if (_pulledObject == _handle || _pulledObject.parent != _commonParent) {
                return FailInitialization("PullTabView handle and pulled object must be different siblings under the same RectTransform parent.");
            }

            if (!_pulledObject.TryGetComponent(out _pulledObjectCanvasGroup)) {
                return FailInitialization("PullTabView pulled object requires a CanvasGroup so hidden content cannot block UI raycasts.");
            }

            _closedAxis = GetMarkerAxis(_startPosition);
            _openAxis = GetMarkerAxis(_stopPosition);
            _disabledAxis = GetMarkerAxis(_disabledPosition);
            if (Mathf.Approximately(_closedAxis, _openAxis)) {
                return FailInitialization("PullTabView start and stop positions must differ on the selected movement axis.");
            }

            _pulledObjectClosedAxis = GetAxis(_pulledObject.localPosition);
            _availabilityState.Value = _initiallyAvailable;
            _openState.Value = false;
            _isValid = true;
            SnapToLogicalPosition();
            RefreshInteraction();
            return true;
        }

        private bool FailInitialization(string message) {
            _isValid = false;
            DisableInteraction();
            Debug.LogError(message, this);
            return false;
        }

        private bool TryGetPointerPosition(PointerEventData eventData, out Vector2 pointerPosition) {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _commonParent,
                eventData.position,
                eventData.pressEventCamera,
                out pointerPosition);
        }

        private float GetMarkerAxis(Transform marker) {
            Vector3 parentLocalPosition = _commonParent.InverseTransformPoint(marker.position);
            return GetAxis(parentLocalPosition);
        }

        private float GetAxis(Vector2 value) {
            return _movementAxis == PullTabAxis.X ? value.x : value.y;
        }

        private float GetAxis(Vector3 value) {
            return _movementAxis == PullTabAxis.X ? value.x : value.y;
        }

        private void ApplyAxisPosition(float handleAxis) {
            Vector3 handlePosition = _handle.localPosition;
            SetAxis(ref handlePosition, handleAxis);
            _handle.localPosition = handlePosition;

            Vector3 pulledObjectPosition = _pulledObject.localPosition;
            SetAxis(ref pulledObjectPosition, _pulledObjectClosedAxis + handleAxis - _closedAxis);
            _pulledObject.localPosition = pulledObjectPosition;
        }

        private void SetAxis(ref Vector3 position, float value) {
            if (_movementAxis == PullTabAxis.X) position.x = value;
            else position.y = value;
        }

        private void SnapToLogicalPosition() {
            float axis = !IsAvailable ? _disabledAxis : IsOpen ? _openAxis : _closedAxis;
            ApplyAxisPosition(axis);
        }

        private void BringToFront() {
            _pulledObject.SetAsLastSibling();
            _handle.SetAsLastSibling();
        }

        private void CompleteSnap() {
            _movementTween = null;
            _isSnapping = false;
            SnapToLogicalPosition();
            RefreshInteraction();
        }

        private void CancelMotion() {
            if (_movementTween != null) {
                _movementTween.Kill(false);
                _movementTween = null;
            }

            _isSnapping = false;
            ResetDrag();
        }

        private void ResetDrag() {
            _isDragging = false;
            _dragStartedOpen = false;
            _activePointerId = default;
        }

        private void RefreshInteraction() {
            if (!_isValid || !isActiveAndEnabled) {
                DisableInteraction();
                return;
            }

            bool handleInteractive = IsAvailable && !_isSnapping;
            _handleCanvasGroup.interactable = handleInteractive;
            _handleCanvasGroup.blocksRaycasts = handleInteractive;

            bool pulledObjectInteractive = IsAvailable && IsOpen && !_isDragging && !_isSnapping;
            _pulledObjectCanvasGroup.interactable = pulledObjectInteractive;
            _pulledObjectCanvasGroup.blocksRaycasts = pulledObjectInteractive;
        }

        private void DisableInteraction() {
            if (_handleCanvasGroup != null) {
                _handleCanvasGroup.interactable = false;
                _handleCanvasGroup.blocksRaycasts = false;
            }

            if (_pulledObjectCanvasGroup == null) return;

            _pulledObjectCanvasGroup.interactable = false;
            _pulledObjectCanvasGroup.blocksRaycasts = false;
        }
    }
}
