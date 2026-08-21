using System;
using Cysharp.Threading.Tasks;
using R3;
using Services;
using Services.Locator;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation {
    public sealed class DocumentQualityTabView : MonoBehaviour {
        private const string TabLabel = "Уровень качества документа";

        private readonly CompositeDisposable _subscriptions = new();

        [Header("Pull Tab")]
        [SerializeField] private PullTabView _pullTab;
        
        [Header("Content")]
        [SerializeField] private Button _increaseButton;
        [SerializeField] private Button _decreaseButton;
        [SerializeField] private TextMeshProUGUI _levelText;
        
        private DocumentQualityTabViewModel _viewModel;

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
            Bind();
            _viewModel = new DocumentQualityTabViewModel(locator.Get<DocumentQualityService>());
            _viewModel.Changed.Subscribe(_ => Refresh()).AddTo(_subscriptions);
            Refresh();
        }

        private void Bind() {
            _increaseButton.onClick.AddListener(Increase);
            _decreaseButton.onClick.AddListener(Decrease);
        }

        private void Decrease() => _viewModel?.Decrease();
        private void Increase() => _viewModel?.Increase();

        private void Refresh() {

            _pullTab.SetAvailable(_viewModel.IsAvailable);
            _levelText.text = (_viewModel.SelectedQualityLevel + 1).ToString();
            _decreaseButton.interactable = _viewModel.SelectedQualityLevel > 0;
            _increaseButton.interactable =
                _viewModel.SelectedQualityLevel < _viewModel.MaximumQualityLevel;
        }

        private void OnDestroy() {
            _increaseButton.onClick.RemoveListener(Increase);
            _decreaseButton.onClick.RemoveListener(Decrease);
            transform.GetComponent<PullTabGroupView>()?.UnregisterTab(_pullTab);
            _subscriptions.Dispose();
            _viewModel?.Dispose();
        }
    }
}
