using System;
using Data.Tutorial;
using R3;
using Services;
using UnityEngine;

namespace Presentation.Tutorial {
    /// <summary>
    /// Mediates between <see cref="TutorialService"/> and the popup view: slide content, typewriter
    /// progress and input-blocking state. Typewriter timing is driven by the view's frame ticks
    /// (<see cref="TickTyping"/>); all reveal logic lives here.
    /// </summary>
    public sealed class TutorialViewModel : IDisposable {
        private readonly TutorialService _service;
        private readonly float _charsPerSecond;
        private readonly CompositeDisposable _disposables = new();

        private readonly ReactiveProperty<bool> _isVisible = new(false);
        private readonly ReactiveProperty<string> _slideText = new(string.Empty);
        private readonly ReactiveProperty<Sprite> _slideIcon = new(null);
        private readonly ReactiveProperty<int> _visibleChars = new(0);
        private readonly ReactiveProperty<bool> _isTyping = new(false);
        private readonly ReactiveProperty<bool> _inputBlocked = new(false);
        private readonly ReactiveProperty<string> _focusTargetId = new(null);

        private string _activeStamp;
        private float _revealAccumulator;

        public ReadOnlyReactiveProperty<bool> IsVisible => _isVisible;
        public ReadOnlyReactiveProperty<string> SlideText => _slideText;
        public ReadOnlyReactiveProperty<Sprite> SlideIcon => _slideIcon;
        public ReadOnlyReactiveProperty<int> VisibleChars => _visibleChars;
        public ReadOnlyReactiveProperty<bool> IsTyping => _isTyping;
        public ReadOnlyReactiveProperty<bool> InputBlocked => _inputBlocked;
        public ReadOnlyReactiveProperty<string> FocusTargetId => _focusTargetId;

        public bool IsCurrentlyTyping => _isTyping.Value;

        public TutorialViewModel(TutorialService service, float charsPerSecond = 30f) {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            if (float.IsNaN(charsPerSecond) || float.IsInfinity(charsPerSecond) || charsPerSecond <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(charsPerSecond),
                    "Characters per second must be finite and positive.");
            }

            _charsPerSecond = charsPerSecond;
            _service.Changed.Subscribe(_ => RefreshFromService()).AddTo(_disposables);
            RefreshFromService();
        }

        public void TickTyping(float unscaledDeltaSeconds) {
            if (!_isTyping.Value) return;
            if (float.IsNaN(unscaledDeltaSeconds) || float.IsInfinity(unscaledDeltaSeconds) ||
                unscaledDeltaSeconds < 0f) {
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaSeconds));
            }

            string text = _slideText.Value ?? string.Empty;
            if (text.Length == 0) {
                CompleteTyping();
                return;
            }

            _revealAccumulator += unscaledDeltaSeconds * _charsPerSecond;
            int target = Mathf.Min(text.Length, Mathf.FloorToInt(_revealAccumulator));
            if (target <= _visibleChars.Value) return;

            _visibleChars.Value = target;
            if (target >= text.Length) CompleteTyping();
        }

        public void HandlePlayerClick() {
            if (!_isVisible.Value) return;
            if (_isTyping.Value) {
                SkipTyping();
                return;
            }

            _service.NotifyInteraction(new TutorialInteractionEvent(TutorialInteractionKind.PlayerClick));
        }

        private void SkipTyping() {
            string text = _slideText.Value ?? string.Empty;
            _revealAccumulator = text.Length;
            _visibleChars.Value = text.Length;
            CompleteTyping();
        }

        private void CompleteTyping() {
            if (!_isTyping.Value) return;

            _isTyping.Value = false;
            _service.NotifyTypingCompleted();
            RefreshBlockingState();
        }

        private void RefreshFromService() {
            if (!_service.HasActive) {
                _activeStamp = null;
                _isVisible.Value = false;
                _isTyping.Value = false;
                _revealAccumulator = 0f;
                _inputBlocked.Value = false;
                _focusTargetId.Value = null;
                return;
            }

            _isVisible.Value = true;
            TutorialDefinition definition = _service.ActiveDefinition;
            int slideIndex = Mathf.Clamp(_service.SlideIndex, 0, definition.Slides.Count - 1);
            TutorialSlide slide = definition.Slides[slideIndex];

            string stamp = $"{definition.Id}#{slideIndex}";
            if (!string.Equals(_activeStamp, stamp, StringComparison.Ordinal)) {
                _activeStamp = stamp;
                _slideText.Value = slide.Text ?? string.Empty;
                _slideIcon.Value = slide.Icon;
                _revealAccumulator = 0f;
                _visibleChars.Value = 0;
                _isTyping.Value = _slideText.Value.Length > 0;
                if (!_isTyping.Value) {
                    _service.NotifyTypingCompleted();
                    return;
                }
            }

            RefreshBlockingState();
        }

        private void RefreshBlockingState() {
            if (!_service.HasActive) {
                _inputBlocked.Value = false;
                _focusTargetId.Value = null;
                return;
            }

            TutorialDefinition definition = _service.ActiveDefinition;
            int slideIndex = Mathf.Clamp(_service.SlideIndex, 0, definition.Slides.Count - 1);
            TutorialSlideCondition condition = definition.Slides[slideIndex].AdvanceCondition;

            _inputBlocked.Value = _isTyping.Value || condition == null || !condition.RequiresInteraction;
            _focusTargetId.Value = !_isTyping.Value && condition != null ? condition.FocusTargetId : null;
        }

        public void Dispose() {
            _disposables.Dispose();
            _isVisible.Dispose();
            _slideText.Dispose();
            _slideIcon.Dispose();
            _visibleChars.Dispose();
            _isTyping.Dispose();
            _inputBlocked.Dispose();
            _focusTargetId.Dispose();
        }
    }
}
