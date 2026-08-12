using System;
using System.Collections.Generic;
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
    public sealed class ResearchView : MonoBehaviour {
        [SerializeField] private PullTabView _pullTab;
        [SerializeField] private Slider _progress;
        [SerializeField] private TextMeshProUGUI _progressText;
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private RectTransform _offerContent;
        [SerializeField] private RectTransform _activeContent;
        [SerializeField] private PracticeCardView _cardPrefab;
        [SerializeField] private Button _sellButton;
        [SerializeField] private TextMeshProUGUI _sellLabel;

        private readonly List<PracticeCardView> _cards = new();
        private readonly CompositeDisposable _subscriptions = new();
        private readonly UnityAction _sellAction;
        private ResearchViewModel _viewModel;

        public ResearchView() => _sellAction = () => _viewModel?.SellOffer();

        private async void Start() {
            if (!ValidateReferences()) { enabled = false; return; }
            ServiceLocator locator = ServiceLocator.For(this);
            try {
                await UniTask.WaitUntil(() => locator != null && locator.IsReady,
                    cancellationToken: this.GetCancellationTokenOnDestroy());
            }
            catch (OperationCanceledException) { return; }
            if (this == null) return;
            _viewModel = new ResearchViewModel(locator.Get<ResearchService>());
            _viewModel.Changed.Subscribe(_ => Refresh()).AddTo(_subscriptions);
            _pullTab.BindAvailability(_viewModel.Availability);
            _sellButton.onClick.AddListener(_sellAction);
            Refresh();
        }

        private void Refresh() {
            if (_viewModel == null) return;
            ClearCards();
            _progress.SetValueWithoutNotify(_viewModel.NormalizedProgress);
            _progressText.text = _viewModel.ProgressText;
            _statusText.text = _viewModel.StatusText;
            _sellButton.interactable = _viewModel.CanSell;
            _sellButton.gameObject.SetActive(_viewModel.Offers.Count > 0);
            _sellLabel.text = $"Sell all: {_viewModel.SalePayout}$";
            for (int index = 0; index < _viewModel.Offers.Count; index++) {
                PracticeCardView card = Instantiate(_cardPrefab, _offerContent, false);
                card.gameObject.SetActive(true);
                card.BindOffer(_viewModel.Offers[index], SelectPractice);
                _cards.Add(card);
            }
            for (int index = 0; index < _viewModel.Active.Count; index++) {
                PracticeCardView card = Instantiate(_cardPrefab, _activeContent, false);
                card.gameObject.SetActive(true);
                card.BindActive(_viewModel.Active[index]);
                _cards.Add(card);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(_offerContent);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_activeContent);
        }

        private void SelectPractice(string id) => _viewModel?.SelectPractice(id);

        private void ClearCards() {
            for (int index = 0; index < _cards.Count; index++) {
                if (_cards[index] == null) continue;
                _cards[index].Unbind();
                Destroy(_cards[index].gameObject);
            }
            _cards.Clear();
        }

        private bool ValidateReferences() {
            bool valid = _pullTab != null && _progress != null && _progressText != null && _statusText != null &&
                         _offerContent != null && _activeContent != null && _cardPrefab != null &&
                         _sellButton != null && _sellLabel != null;
            if (!valid) Debug.LogError("ResearchView requires all pull-tab, progress, content, card, status, and sell references.", this);
            return valid;
        }

        private void OnDestroy() {
            _sellButton?.onClick.RemoveListener(_sellAction);
            _pullTab?.UnbindAvailability();
            _subscriptions.Dispose();
            ClearCards();
            _viewModel?.Dispose();
            _viewModel = null;
        }
    }
}
