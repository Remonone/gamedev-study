using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Services;
using Services.Locator;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Presentation {
    public sealed class StartingSignatureSelectionView : MonoBehaviour {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button[] _buttons = new Button[4];
        [SerializeField] private TextMeshProUGUI[] _labels = new TextMeshProUGUI[4];
        [SerializeField] private Color _previewColor = Color.white;
        [SerializeField, Range(0f, 0.45f)] private float _previewPadding = 0.1f;

        private readonly UnityAction[] _actions = new UnityAction[4];
        private readonly SignatureGraphic[] _previews = new SignatureGraphic[4];
        private StartingSignatureSelectionViewModel _viewModel;

        private async void Start() {
            if (!HasRequiredReferences()) {
                Debug.LogError("StartingSignatureSelectionView requires a panel and exactly four button/label pairs.", this);
                enabled = false;
                return;
            }

            ServiceLocator locator = ServiceLocator.For(this);
            try {
                await UniTask.WaitUntil(() => locator != null && locator.IsInitializationComplete,
                    cancellationToken: this.GetCancellationTokenOnDestroy());
            } catch (OperationCanceledException) {
                return;
            }
            if (locator.InitializationException != null) return;

            _viewModel = new StartingSignatureSelectionViewModel(locator.Get<SignatureProgressionService>());
            _panel.SetActive(_viewModel.IsSelectionRequired);
            if (!_viewModel.IsSelectionRequired) return;
            Canvas.ForceUpdateCanvases();
            if (_viewModel.Options.Count != 4) {
                Debug.LogError("Starting signature selection requires exactly four options.", this);
                return;
            }

            for (int index = 0; index < 4; index++) {
                int capturedIndex = index;
                RenderOption(index, _viewModel.Options[index]);
                _actions[index] = () => Select(capturedIndex);
                _buttons[index].onClick.AddListener(_actions[index]);
            }
        }

        private void Select(int index) {
            if (_viewModel != null && _viewModel.Select(index)) _panel.SetActive(false);
        }

        private void OnDestroy() {
            for (int index = 0; index < _actions.Length; index++) {
                if (_actions[index] != null && _buttons != null && index < _buttons.Length && _buttons[index] != null) {
                    _buttons[index].onClick.RemoveListener(_actions[index]);
                }
            }
            _viewModel?.Dispose();
            _viewModel = null;
        }

        private bool HasRequiredReferences() {
            if (_panel == null || _buttons == null || _labels == null || _buttons.Length != 4 || _labels.Length != 4)
                return false;

            for (int index = 0; index < 4; index++) {
                if (_buttons[index] == null || _labels[index] == null) return false;
            }

            return true;
        }

        private void RenderOption(int index, StartingSignatureOption option) {
            _labels[index].text = $"{option.CategoryDisplayName}\n{option.DisplayName}\nBase income: {option.BaseIncomeText}";

            SignatureGraphic preview = GetOrCreatePreview(index);
            if (option.HasPreview && TryBuildLocalStrokes(preview.rectTransform.rect, option.PreviewStrokes,
                    out List<IReadOnlyList<Vector2>> localStrokes)) {
                _labels[index].enabled = true;
                preview.enabled = true;
                preview.color = _previewColor;
                preview.SetStrokes(localStrokes);
                return;
            }

            _labels[index].enabled = true;
            preview.Clear();
            preview.enabled = false;
        }

        private SignatureGraphic GetOrCreatePreview(int index) {
            if (_previews[index] != null) return _previews[index];

            RectTransform labelRect = _labels[index].rectTransform;
            var previewObject = new GameObject("Signature Preview", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(SignatureGraphic));
            previewObject.layer = _labels[index].gameObject.layer;

            var previewRect = (RectTransform)previewObject.transform;
            previewRect.SetParent(labelRect, false);
            previewRect.anchorMin = Vector2.zero;
            previewRect.anchorMax = Vector2.one;
            previewRect.offsetMin = Vector2.zero;
            previewRect.offsetMax = Vector2.zero;
            previewRect.pivot = labelRect.pivot;

            SignatureGraphic preview = previewObject.GetComponent<SignatureGraphic>();
            preview.raycastTarget = false;
            preview.color = _previewColor;
            _previews[index] = preview;
            return preview;
        }

        private bool TryBuildLocalStrokes(Rect targetRect, IReadOnlyList<IReadOnlyList<Vector2>> normalizedStrokes,
            out List<IReadOnlyList<Vector2>> localStrokes) {
            localStrokes = new List<IReadOnlyList<Vector2>>();
            if (normalizedStrokes == null || normalizedStrokes.Count == 0 || targetRect.width <= 0f ||
                targetRect.height <= 0f) return false;

            float padding = Mathf.Clamp(_previewPadding, 0f, 0.45f);
            float xMin = Mathf.Lerp(targetRect.xMin, targetRect.xMax, padding);
            float xMax = Mathf.Lerp(targetRect.xMin, targetRect.xMax, 1f - padding);
            float yMin = Mathf.Lerp(targetRect.yMin, targetRect.yMax, padding);
            float yMax = Mathf.Lerp(targetRect.yMin, targetRect.yMax, 1f - padding);

            for (int strokeIndex = 0; strokeIndex < normalizedStrokes.Count; strokeIndex++) {
                IReadOnlyList<Vector2> normalizedStroke = normalizedStrokes[strokeIndex];
                if (normalizedStroke == null || normalizedStroke.Count == 0) continue;

                var localStroke = new List<Vector2>(normalizedStroke.Count);
                for (int pointIndex = 0; pointIndex < normalizedStroke.Count; pointIndex++) {
                    Vector2 normalizedPoint = normalizedStroke[pointIndex];
                    localStroke.Add(new Vector2(
                        Mathf.Lerp(xMin, xMax, normalizedPoint.x),
                        Mathf.Lerp(yMin, yMax, normalizedPoint.y)));
                }
                localStrokes.Add(localStroke);
            }

            return localStrokes.Count > 0;
        }
    }
}
