using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace UI {
    public class RewardIncomeDisplay : MonoBehaviour {
        [SerializeField] private TMP_Text _text;
        [SerializeField] private Vector2 _defaultSize = new(220f, 64f);
        [SerializeField] private Vector2 _drift = new(0f, 48f);
        [SerializeField, Min(0f)] private float _visibleDuration = 0.75f;
        [SerializeField, Min(0f)] private float _fadeDuration = 0.35f;
        [SerializeField] private Color _acceptedColor = new(0.51989424f, 0.8642394f, 0.37046114f, 1f);
        [SerializeField] private Color _rejectedColor = new(0.95f, 0.33f, 0.28f, 1f);

        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Sequence _sequence;
        private Action<RewardIncomeDisplay> _release;

        public RectTransform RectTransform {
            get {
                EnsureInitialized();
                return _rectTransform;
            }
        }

        private void Awake() {
            EnsureInitialized();
        }

        private void OnDisable() {
            KillMotion();
        }

        private void OnDestroy() {
            KillMotion();
            _release = null;
        }

        public void SetReleaseCallback(Action<RewardIncomeDisplay> release) {
            _release = release;
        }

        public void Show(float accuracy, bool isAccepted, string acceptedText) {
            EnsureInitialized();
            KillMotion();

            _canvasGroup.alpha = 1f;
            _rectTransform.localScale = Vector3.one;
            _text.color = isAccepted ? _acceptedColor : _rejectedColor;
            _text.text = $"{(isAccepted ? acceptedText : "Rejected")}\nAcc.: {(accuracy * 100f):0.00}%";

            Vector2 startPosition = _rectTransform.anchoredPosition;
            float totalDuration = _visibleDuration + _fadeDuration;
            _sequence = DOTween.Sequence()
                .SetTarget(this)
                .Append(DOTween
                    .To(
                        () => _rectTransform.anchoredPosition,
                        value => _rectTransform.anchoredPosition = value,
                        startPosition + _drift,
                        totalDuration)
                    .SetEase(Ease.OutCubic))
                .Insert(
                    _visibleDuration,
                    DOTween.To(
                        () => _canvasGroup.alpha,
                        value => _canvasGroup.alpha = value,
                        0f,
                        _fadeDuration))
                .OnComplete(Release);
        }

        public void ResetForPool() {
            EnsureInitialized();
            KillMotion();
            _canvasGroup.alpha = 0f;
            _rectTransform.localScale = Vector3.one;
            gameObject.SetActive(false);
        }

        private void Release() {
            _sequence = null;
            _release?.Invoke(this);
        }

        private void KillMotion() {
            if (_sequence == null) return;
            _sequence.Kill();
            _sequence = null;
        }

        private void EnsureInitialized() {
            if (_rectTransform == null) {
                _rectTransform = transform as RectTransform;
                if (_rectTransform == null) {
                    _rectTransform = gameObject.AddComponent<RectTransform>();
                }

                if (_rectTransform.sizeDelta.sqrMagnitude <= 0.01f ||
                    _rectTransform.sizeDelta.x < _defaultSize.x ||
                    _rectTransform.sizeDelta.y < _defaultSize.y) {
                    _rectTransform.sizeDelta = _defaultSize;
                }

                _rectTransform.localScale = Vector3.one;
            }

            if (_canvasGroup == null && !TryGetComponent(out _canvasGroup)) {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (_text == null || !(_text is TextMeshProUGUI)) {
                _text = ResolveUiText(_text);
            }
        }

        private TMP_Text ResolveUiText(TMP_Text source) {
            if (TryGetComponent(out TextMeshProUGUI rootText)) return rootText;

            TextMeshProUGUI childText = GetComponentInChildren<TextMeshProUGUI>(true);
            if (childText != null) return childText;

            if (source == null) source = GetComponent<TMP_Text>();
            if (source != null && !(source is TextMeshProUGUI)) {
                source.enabled = false;
                if (source.TryGetComponent(out Renderer sourceRenderer)) {
                    sourceRenderer.enabled = false;
                }
            }

            var textObject = new GameObject("RewardTextUI", typeof(RectTransform));
            textObject.layer = gameObject.layer;
            textObject.transform.SetParent(transform, false);

            var textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var uiText = textObject.AddComponent<TextMeshProUGUI>();
            uiText.raycastTarget = false;
            uiText.alignment = source != null ? source.alignment : TextAlignmentOptions.Center;
            uiText.color = source != null ? source.color : _acceptedColor;
            uiText.fontSize = source != null ? source.fontSize : 36f;
            uiText.fontStyle = source != null ? source.fontStyle : FontStyles.Normal;
            uiText.enableAutoSizing = source != null && source.enableAutoSizing;
            uiText.richText = source == null || source.richText;
            uiText.text = source != null ? source.text : string.Empty;
            if (source != null && source.font != null) uiText.font = source.font;
            return uiText;
        }
    }
}
