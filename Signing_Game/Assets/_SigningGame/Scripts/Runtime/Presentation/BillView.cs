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
    public sealed class BillView : MonoBehaviour {
        [Header("Pull Tab")]
        [SerializeField] private PullTabView _pullTab;

        [Header("Internal Tabs")]
        [SerializeField] private Button _catalogTab;
        [SerializeField] private Button _activeTab;
        [SerializeField] private Button _completedTab;

        [Header("List")]
        [SerializeField] private RectTransform _content;
        [SerializeField] private BillCardView _cardPrefab;
        [SerializeField] private TextMeshProUGUI _emptyMessage;
        [SerializeField] private BillTooltipView _tooltip;

        private readonly List<BillCardView> _cards = new();
        private readonly CompositeDisposable _subscriptions = new();
        private readonly UnityAction _catalogAction;
        private readonly UnityAction _activeAction;
        private readonly UnityAction _completedAction;
        private BillViewModel _viewModel;

        public BillView() {
            _catalogAction = () => _viewModel?.SelectTab(BillTab.Catalog);
            _activeAction = () => _viewModel?.SelectTab(BillTab.Active);
            _completedAction = () => _viewModel?.SelectTab(BillTab.Completed);
        }

        private async void Start() {
            if (!ValidateReferences()) {
                enabled = false;
                return;
            }
            ServiceLocator locator = ServiceLocator.For(this);
            try {
                await UniTask.WaitUntil(() => locator != null && locator.IsReady,
                    cancellationToken: this.GetCancellationTokenOnDestroy());
            }
            catch (OperationCanceledException) {
                return;
            }
            if (this == null) return;
            _viewModel = new BillViewModel(locator.Get<BillService>());
            _viewModel.Changed.Subscribe(_ => Refresh()).AddTo(_subscriptions);
            _pullTab.OpenState.Where(open => !open).Subscribe(_ => _tooltip.Hide()).AddTo(_subscriptions);
            _pullTab.BindAvailability(_viewModel.Availability);
            _catalogTab.onClick.AddListener(_catalogAction);
            _activeTab.onClick.AddListener(_activeAction);
            _completedTab.onClick.AddListener(_completedAction);
            Refresh();
        }

        private void Refresh() {
            if (_viewModel == null) return;
            ClearCards();
            BillTab tab = _viewModel.SelectedTab.CurrentValue;
            IReadOnlyList<BillCardPresentationModel> models = tab switch {
                BillTab.Catalog => _viewModel.Catalog,
                BillTab.Active => _viewModel.Active,
                BillTab.Completed => _viewModel.Completed,
                _ => Array.Empty<BillCardPresentationModel>()
            };
            for (int index = 0; index < models.Count; index++) {
                BillCardView card = Instantiate(_cardPrefab, _content, false);
                card.gameObject.SetActive(true);
                card.Bind(
                    models[index],
                    Purchase,
                    Toggle,
                    SetPriority,
                    _tooltip.Show,
                    _tooltip.Hide);
                _cards.Add(card);
            }
            _emptyMessage.gameObject.SetActive(models.Count == 0);
            if (models.Count == 0) _emptyMessage.text = _viewModel.GetEmptyMessage(tab);
            _catalogTab.interactable = tab != BillTab.Catalog;
            _activeTab.interactable = tab != BillTab.Active;
            _completedTab.interactable = tab != BillTab.Completed;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }

        private void Purchase(long optionId) => _viewModel?.Purchase(optionId);
        private void Toggle(long completionOrder) => _viewModel?.ToggleCompletion(completionOrder);
        private void SetPriority(long instanceId, int priority) => _viewModel?.SetPriority(instanceId, priority);

        private void ClearCards() {
            _tooltip?.Hide();
            for (int index = 0; index < _cards.Count; index++) {
                if (_cards[index] == null) continue;
                _cards[index].Unbind();
                Destroy(_cards[index].gameObject);
            }
            _cards.Clear();
        }

        private bool ValidateReferences() {
            bool valid = _pullTab != null && _catalogTab != null && _activeTab != null &&
                         _completedTab != null && _content != null && _cardPrefab != null &&
                         _emptyMessage != null && _tooltip != null;
            if (!valid) Debug.LogError("BillView requires all pull-tab, tab, list, card, empty-state, and tooltip references.", this);
            return valid;
        }

        private void OnDestroy() {
            _catalogTab?.onClick.RemoveListener(_catalogAction);
            _activeTab?.onClick.RemoveListener(_activeAction);
            _completedTab?.onClick.RemoveListener(_completedAction);
            _subscriptions.Dispose();
            ClearCards();
            _viewModel?.Dispose();
            _viewModel = null;
        }
    }
}
