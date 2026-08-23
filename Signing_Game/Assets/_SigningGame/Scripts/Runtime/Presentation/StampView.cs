using System;
using System.Collections.Generic;
using DG.Tweening;
using R3;
using Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Presentation {
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup), typeof(Image))]
    public sealed class StampView : MonoBehaviour, IDisposable, IPointerDownHandler {
        private const float StampWidth = 140f;
        private const float StampHeight = 90f;
        private const float BottomMargin = 45f;
        private const float SlideDuration = 0.35f;

        private readonly Vector3[] _worldCorners = new Vector3[4];
        private readonly List<DocumentView> _documents = new();

        private RectTransform _rectTransform;
        private RectTransform _canvasRect;
        private CanvasGroup _canvasGroup;
        private Canvas _canvas;
        private UnlockService _unlocks;
        private IDisposable _unlockObservation;
        private Tween _movement;
        private Vector2 _visiblePosition;
        private Vector2 _hiddenPosition;
        private bool _isUnlocked;
        private bool _isHeld;
        private bool _initialized;
        private bool _disposed;

        private void Awake() {
            _rectTransform = (RectTransform)transform;
            _canvasGroup = GetComponent<CanvasGroup>()
                ?? throw new InvalidOperationException("StampView requires a CanvasGroup.");
            if (GetComponent<Image>() == null) {
                throw new InvalidOperationException("StampView requires an Image.");
            }
        }

        public void Initialize(UnlockService unlocks, Canvas canvas) {
            if (_initialized) throw new InvalidOperationException("StampView can only be initialized once.");
            _unlocks = unlocks ?? throw new ArgumentNullException(nameof(unlocks));
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            _canvasRect = _canvas.transform as RectTransform
                ?? throw new InvalidOperationException("StampView requires a RectTransform canvas.");

            _rectTransform.anchorMin = new Vector2(0.5f, 0f);
            _rectTransform.anchorMax = new Vector2(0.5f, 0f);
            _rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _rectTransform.sizeDelta = new Vector2(StampWidth, StampHeight);
            _visiblePosition = new Vector2(0f, BottomMargin + StampHeight * 0.5f);
            _hiddenPosition = new Vector2(0f, _visiblePosition.y - StampHeight * 1.5f);
            _rectTransform.anchoredPosition = _hiddenPosition;

            _isUnlocked = _unlocks.IsUnlocked(Constants.FeatureIds.Stamp);
            _unlockObservation = _unlocks.Changed.Subscribe(_ => OnUnlockChanged());
            _initialized = true;
            SetUnlocked(_isUnlocked, _isUnlocked);
        }

        public void Dispose() {
            if (_disposed) return;
            _disposed = true;
            _unlockObservation?.Dispose();
            _unlockObservation = null;
            CancelHold();
            _movement?.Kill();
            _movement = null;
            _documents.Clear();
            _unlocks = null;
            _canvas = null;
            _canvasRect = null;
        }

        private void Update() {
            if (!_initialized || _disposed || !_isHeld) return;
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            MoveToPointer(mouse.position.ReadValue());
            if (mouse.rightButton.wasPressedThisFrame) TryPlaceStamp();
            if (!mouse.leftButton.isPressed) ReleaseHold();
        }

        internal void BeginHold() {
            if (!_initialized || _disposed || !_isUnlocked || _isHeld) return;
            _movement?.Kill();
            _movement = null;
            _isHeld = true;
            _canvasGroup.blocksRaycasts = false;
            if (Mouse.current != null) MoveToPointer(Mouse.current.position.ReadValue());
        }

        public void OnPointerDown(PointerEventData eventData) {
            if (eventData.button == PointerEventData.InputButton.Left) BeginHold();
        }

        private void OnUnlockChanged() {
            if (!_initialized || _unlocks == null) return;
            bool unlocked = _unlocks.IsUnlocked(Constants.FeatureIds.Stamp);
            if (_isUnlocked == unlocked) return;
            SetUnlocked(unlocked, true);
        }

        private void SetUnlocked(bool unlocked, bool animate) {
            _isUnlocked = unlocked;
            if (!unlocked) CancelHold();

            _movement?.Kill();
            _canvasGroup.alpha = unlocked ? 1f : 0f;
            _canvasGroup.interactable = unlocked;
            _canvasGroup.blocksRaycasts = unlocked;
            Vector2 target = unlocked ? _visiblePosition : _hiddenPosition;
            if (!animate) {
                _rectTransform.anchoredPosition = target;
                _movement = null;
                return;
            }

            Vector2 start = _rectTransform.anchoredPosition;
            _movement = DOVirtual.Float(0f, 1f, SlideDuration, progress => {
                    _rectTransform.anchoredPosition = Vector2.LerpUnclamped(start, target, progress);
                });
        }

        private void MoveToPointer(Vector2 screenPosition) {
            if (_canvasRect == null || _canvas == null) return;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    _canvasRect,
                    screenPosition,
                    _canvas.worldCamera,
                    out Vector3 worldPosition)) {
                _rectTransform.position = worldPosition;
            }
        }

        private void ReleaseHold() {
            if (!_isHeld) return;
            _isHeld = false;
            _canvasGroup.blocksRaycasts = _isUnlocked;
        }

        private void CancelHold() {
            ReleaseHold();
        }

        private void TryPlaceStamp() {
            if (_canvas == null) return;
            Rect stampScreenRect = GetScreenRect();
            _documents.Clear();
            _canvas.GetComponentsInChildren(true, _documents);

            DocumentView topmost = null;
            for (int index = 0; index < _documents.Count; index++) {
                DocumentView candidate = _documents[index];
                if (!candidate.TryGetScreenRect(_canvas.worldCamera, out Rect documentScreenRect) ||
                    !HasPositiveIntersection(stampScreenRect, documentScreenRect)) {
                    continue;
                }

                if (topmost == null || IsHigherSibling(candidate.transform, topmost.transform)) {
                    topmost = candidate;
                }
            }

            topmost?.TryPlaceStamp(stampScreenRect, _canvas.worldCamera);
        }

        private Rect GetScreenRect() {
            _rectTransform.GetWorldCorners(_worldCorners);
            Vector2 first = RectTransformUtility.WorldToScreenPoint(_canvas.worldCamera, _worldCorners[0]);
            float xMin = first.x;
            float xMax = first.x;
            float yMin = first.y;
            float yMax = first.y;
            for (int index = 1; index < _worldCorners.Length; index++) {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(_canvas.worldCamera, _worldCorners[index]);
                xMin = Mathf.Min(xMin, point.x);
                xMax = Mathf.Max(xMax, point.x);
                yMin = Mathf.Min(yMin, point.y);
                yMax = Mathf.Max(yMax, point.y);
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static bool HasPositiveIntersection(Rect first, Rect second) {
            return Mathf.Min(first.xMax, second.xMax) > Mathf.Max(first.xMin, second.xMin) &&
                   Mathf.Min(first.yMax, second.yMax) > Mathf.Max(first.yMin, second.yMin);
        }

        private static bool IsHigherSibling(Transform candidate, Transform current) {
            if (candidate.parent == current.parent) {
                return candidate.GetSiblingIndex() > current.GetSiblingIndex();
            }

            return candidate.GetSiblingIndex() > current.GetSiblingIndex();
        }

        private void OnDestroy() {
            Dispose();
        }
    }
}
