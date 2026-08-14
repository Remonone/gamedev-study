using System;
using Cysharp.Threading.Tasks;
using R3;
using Services;
using Services.Locator;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Presentation {
    public sealed class MainMenuView : MonoBehaviour {
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _quitButton;
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private AudioSettingsView _audioSettingsView;
        [SerializeField] private TextMeshProUGUI _errorText;

        private readonly CompositeDisposable _subscriptions = new();
        private MainMenuViewModel _viewModel;
        private UnityAction _playAction;
        private UnityAction _settingsAction;
        private UnityAction _quitAction;

        private async void Start() {
            ServiceLocator locator = ServiceLocator.Application;
            try {
                await UniTask.WaitUntil(() => locator != null && locator.IsInitializationComplete,
                    cancellationToken: this.GetCancellationTokenOnDestroy());
            } catch (OperationCanceledException) {
                return;
            }

            if (locator.InitializationException != null) {
                Debug.LogException(locator.InitializationException, this);
                return;
            }

            _viewModel = new MainMenuViewModel(locator.Get<SceneFlowService>());
            _playAction = _viewModel.Play;
            _settingsAction = _viewModel.OpenSettings;
            _quitAction = _viewModel.Quit;
            _playButton.onClick.AddListener(_playAction);
            _settingsButton.onClick.AddListener(_settingsAction);
            _quitButton.onClick.AddListener(_quitAction);
            _audioSettingsView.Bind(locator.Get<AudioSettingsService>(), _viewModel.CloseSettings);
            _viewModel.SettingsVisible.Subscribe(SetSettingsVisible).AddTo(_subscriptions);
            _viewModel.Loading.Subscribe(SetBusy).AddTo(_subscriptions);
            _viewModel.LastError.Subscribe(SetError).AddTo(_subscriptions);
        }

        private void SetSettingsVisible(bool visible) => _settingsPanel.SetActive(visible);

        private void SetBusy(bool busy) {
            _playButton.interactable = !busy;
            _settingsButton.interactable = !busy;
            _quitButton.interactable = !busy;
        }

        private void SetError(string message) {
            if (_errorText == null) return;
            _errorText.text = message ?? string.Empty;
            _errorText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
        }

        private void OnDestroy() {
            if (_playAction != null) _playButton?.onClick.RemoveListener(_playAction);
            if (_settingsAction != null) _settingsButton?.onClick.RemoveListener(_settingsAction);
            if (_quitAction != null) _quitButton?.onClick.RemoveListener(_quitAction);
            _audioSettingsView?.Unbind();
            _subscriptions.Dispose();
            _viewModel?.Dispose();
        }
    }
}
