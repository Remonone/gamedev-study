using System;
using Cysharp.Threading.Tasks;
using R3;
using Services;
using Services.Locator;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Presentation {
    public sealed class GameMenuTabView : MonoBehaviour {
        private readonly CompositeDisposable _subscriptions = new();

        private GameplaySettingsPopupView _settingsPopup;
        [SerializeField] private PullTabView _pullTab;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _guidanceButton;
        [SerializeField] private Button _saveButton;
        private GameMenuTabViewModel _viewModel;

        private async void Start() {
            ServiceLocator locator = ServiceLocator.For(this);
            try {
                await UniTask.WaitUntil(
                    () => locator != null && locator.IsInitializationComplete,
                    cancellationToken: this.GetCancellationTokenOnDestroy());
            }
            catch (OperationCanceledException) {
                return;
            }

            if (this == null || locator.InitializationException != null) return;

            BuildUi(locator);
            _settingsButton.onClick.AddListener(OpenSettings);
            _guidanceButton.onClick.AddListener(RequestGuidance);
            _saveButton.onClick.AddListener(SaveAndExit);
            _viewModel = new GameMenuTabViewModel(
                locator.Get<SignatureGuidanceDocumentProducer>(),
                locator.Get<SaveService>(),
                locator.Get<SceneFlowService>());
            _viewModel.SettingsVisible.Subscribe(_settingsPopup.SetVisible).AddTo(_subscriptions);
        }

        private void BuildUi(ServiceLocator locator) {
            var popupObject = new GameObject("Gameplay Settings Popup", typeof(RectTransform));
            popupObject.transform.SetParent(transform, false);
            _settingsPopup = popupObject.AddComponent<GameplaySettingsPopupView>();
            _settingsPopup.Bind(locator.Get<AudioSettingsService>(), CloseSettings);
        }

        private void OpenSettings() => _viewModel?.OpenSettings();

        private void CloseSettings() => _viewModel?.CloseSettings();

        private void RequestGuidance() => _viewModel?.RequestSignatureGuidance();

        private void SaveAndExit() => _viewModel?.SaveAndExit();

        private void OnDestroy() {
            _settingsButton.onClick.RemoveListener(OpenSettings);
            _guidanceButton.onClick.RemoveListener(RequestGuidance);
            _saveButton.onClick.RemoveListener(SaveAndExit);
            transform.GetComponent<PullTabGroupView>()?.UnregisterTab(_pullTab);
            _subscriptions.Dispose();
            _settingsPopup?.Unbind();
            _viewModel?.Dispose();
        }
    }
}
