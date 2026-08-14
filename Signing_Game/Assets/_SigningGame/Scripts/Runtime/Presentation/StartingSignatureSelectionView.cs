using System;
using Cysharp.Threading.Tasks;
using Services;
using Services.Locator;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Presentation {
    public sealed class StartingSignatureSelectionView : MonoBehaviour {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button[] _buttons = new Button[3];
        [SerializeField] private TextMeshProUGUI[] _labels = new TextMeshProUGUI[3];

        private readonly UnityAction[] _actions = new UnityAction[3];
        private StartingSignatureSelectionViewModel _viewModel;

        private async void Start() {
            if (_panel == null || _buttons == null || _labels == null || _buttons.Length != 3 || _labels.Length != 3) {
                Debug.LogError("StartingSignatureSelectionView requires a panel and exactly three button/label pairs.", this);
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
            if (_viewModel.Options.Count != 3) {
                Debug.LogError("Starting signature selection requires exactly three options.", this);
                return;
            }

            for (int index = 0; index < 3; index++) {
                int capturedIndex = index;
                _labels[index].text = _viewModel.Options[index].DisplayName;
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
    }
}
