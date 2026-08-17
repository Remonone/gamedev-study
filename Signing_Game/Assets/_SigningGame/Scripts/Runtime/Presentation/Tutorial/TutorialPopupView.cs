using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using R3;
using Services;
using Services.Locator;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Presentation.Tutorial {
    /// <summary>
    /// Renders the active tutorial popup: dim, bottom panel, typewriter text, optional speaker icon.
    /// The root stays active with hidden visuals; visibility is driven entirely by the view model.
    /// Click handling lives here so bubbled clicks from any popup surface behave identically.
    /// </summary>
    public sealed class TutorialPopupView : MonoBehaviour, IPointerClickHandler {
        [Header("References")]
        [SerializeField] private CanvasGroup _rootCanvasGroup;
        [SerializeField] private Graphic _dim;
        [SerializeField] private RectTransform _panel;
        [SerializeField] private Graphic _panelBackground;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private Image _icon;

        [Header("Focus Bridges")]
        [SerializeField] private TutorialTabBridge _tabBridge;
        [SerializeField] private TutorialDocumentCollectorBridge _collectorBridge;

        [Header("Typewriter")]
        [SerializeField, Min(1f)] private float _charsPerSecond = 30f;

        [Header("Animation")]
        [SerializeField, Min(0f)] private float _fadeDuration = 0.15f;
        [SerializeField, Min(0f)] private float _slideUpDuration = 0.25f;
        [SerializeField, Min(0f)] private float _hiddenPanelOvershoot = 64f;

        private TutorialViewModel _viewModel;
        private CompositeDisposable _subscriptions;
        private Tween _fadeTween;
        private Tween _panelTween;
        private bool _shown;

        public void OnPointerClick(PointerEventData eventData) {
            _viewModel?.HandlePlayerClick();
        }

        private void Awake() {
            if (ValidateReferences()) ApplyHiddenState(true);
        }

        private async void Start() {
            ServiceLocator locator = ServiceLocator.For(this);
            try {
                await UniTask.WaitUntil(() => locator != null && locator.IsReady,
                    cancellationToken: this.GetCancellationTokenOnDestroy());
            } catch (OperationCanceledException) {
                return;
            }

            if (this == null || !locator.TryGet(out TutorialService tutorial)) return;

            _viewModel = new TutorialViewModel(tutorial, _charsPerSecond);
            _subscriptions = new CompositeDisposable();
            _viewModel.IsVisible.Subscribe(OnVisibilityChanged).AddTo(_subscriptions);
            _viewModel.SlideText.Subscribe(OnSlideTextChanged).AddTo(_subscriptions);
            _viewModel.SlideIcon.Subscribe(OnSlideIconChanged).AddTo(_subscriptions);
            _viewModel.VisibleChars.Subscribe(OnVisibleCharsChanged).AddTo(_subscriptions);
            _viewModel.InputBlocked.Subscribe(OnInputBlockedChanged).AddTo(_subscriptions);
            _viewModel.FocusTargetId.Subscribe(OnFocusTargetChanged).AddTo(_subscriptions);
        }

        private void Update() {
            if (_viewModel != null && _viewModel.IsCurrentlyTyping) {
                _viewModel.TickTyping(Time.unscaledDeltaTime);
            }
        }

        private void OnDestroy() {
            _fadeTween?.Kill();
            _panelTween?.Kill();
            _subscriptions?.Dispose();
            _subscriptions = null;
            _viewModel?.Dispose();
            _viewModel = null;
            StopFocusPulse();
        }

        private void OnVisibilityChanged(bool visible) {
            if (visible == _shown) return;

            _shown = visible;
            _fadeTween?.Kill();
            _panelTween?.Kill();
            float hiddenY = -(_panel.rect.height + _hiddenPanelOvershoot);

            if (visible) {
                _rootCanvasGroup.interactable = true;
                _fadeTween = DOTween.To(() => _rootCanvasGroup.alpha, value => _rootCanvasGroup.alpha = value,
                    1f, _fadeDuration);
                _panel.anchoredPosition = new Vector2(_panel.anchoredPosition.x, hiddenY);
                _panelTween = DOTween.To(() => _panel.anchoredPosition.y,
                        value => _panel.anchoredPosition = new Vector2(_panel.anchoredPosition.x, value),
                        0f, _slideUpDuration)
                    .SetEase(Ease.OutCubic);
                return;
            }

            StopFocusPulse();
            _rootCanvasGroup.interactable = false;
            SetRaycastBlocking(false);
            _fadeTween = DOTween.To(() => _rootCanvasGroup.alpha, value => _rootCanvasGroup.alpha = value,
                0f, _fadeDuration);
            _panelTween = DOTween.To(() => _panel.anchoredPosition.y,
                    value => _panel.anchoredPosition = new Vector2(_panel.anchoredPosition.x, value),
                    hiddenY, _slideUpDuration)
                .SetEase(Ease.InCubic);
        }

        private void OnSlideTextChanged(string text) {
            _text.text = text ?? string.Empty;
            _text.maxVisibleCharacters = int.MaxValue;
        }

        private void OnSlideIconChanged(Sprite icon) {
            bool hasIcon = icon != null;
            _icon.gameObject.SetActive(hasIcon);
            if (hasIcon) {
                _icon.sprite = icon;
                _icon.preserveAspect = true;
            }
        }

        private void OnVisibleCharsChanged(int visibleChars) {
            _text.maxVisibleCharacters = visibleChars;
        }

        private void OnInputBlockedChanged(bool blocked) {
            if (!_shown) return;
            SetRaycastBlocking(blocked);
        }

        private void OnFocusTargetChanged(string focusTargetId) {
            StopFocusPulse();
            if (string.IsNullOrEmpty(focusTargetId)) return;

            if (string.Equals(focusTargetId, Constants.TutorialIds.DocumentCollector,
                    StringComparison.Ordinal)) {
                if (_collectorBridge == null) {
                    Debug.LogWarning(
                        "Tutorial popup cannot highlight the document collector: no collector bridge is assigned.", this);
                    return;
                }

                _collectorBridge.Pulse();
                return;
            }

            if (_tabBridge == null) {
                Debug.LogWarning(
                    $"Tutorial popup cannot highlight focus target '{focusTargetId}': no tab bridge is assigned.", this);
                return;
            }

            _tabBridge.Pulse(focusTargetId);
        }

        private void SetRaycastBlocking(bool blocked) {
            if (_dim != null) _dim.raycastTarget = blocked;
            if (_panelBackground != null) _panelBackground.raycastTarget = blocked;
        }

        private void StopFocusPulse() {
            _tabBridge?.StopPulse();
            _collectorBridge?.StopPulse();
        }

        private void ApplyHiddenState(bool immediate) {
            _shown = false;
            _rootCanvasGroup.interactable = false;
            SetRaycastBlocking(false);
            if (immediate) {
                _rootCanvasGroup.alpha = 0f;
                _panel.anchoredPosition = new Vector2(_panel.anchoredPosition.x,
                    -(_panel.rect.height + _hiddenPanelOvershoot));
            }
        }

        private bool ValidateReferences() {
            bool valid = _rootCanvasGroup != null && _dim != null && _panel != null &&
                         _panelBackground != null && _text != null && _icon != null;
            if (valid) return true;

            Debug.LogError("TutorialPopupView requires root canvas group, dim, panel, background, text and icon references.",
                this);
            return false;
        }
    }
}
