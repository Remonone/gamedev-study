using System;
using System.Collections.Generic;
using System.Globalization;
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
    public sealed class OfficeView : MonoBehaviour {
        [Header("Pull Tab")]
        [SerializeField] private PullTabView _pullTab;

        [Header("Summary")]
        [SerializeField] private TextMeshProUGUI _staffText;
        [SerializeField] private TextMeshProUGUI _pendingText;
        [SerializeField] private TextMeshProUGUI _speedText;
        [SerializeField] private TextMeshProUGUI _qualityText;
        [SerializeField] private TextMeshProUGUI _acceptanceText;
        [SerializeField] private TextMeshProUGUI _rewardText;

        [Header("Slots")]
        [SerializeField] private RectTransform _slotViewport;
        [SerializeField] private RectTransform _slotContent;
        [SerializeField] private GridLayoutGroup _slotGrid;
        [SerializeField] private OfficeSlotView _slotPrefab;
        [SerializeField, Min(180f)] private float _minimumSlotWidth = 280f;
        [SerializeField, Min(180f)] private float _slotHeight = 250f;

        [Header("Tooltip")]
        [SerializeField] private OfficeTooltipView _tooltip;

        [Header("Bid Popup")]
        [SerializeField] private GameObject _bidPopupRoot;
        [SerializeField] private Slider _bidSlider;
        [SerializeField] private TextMeshProUGUI _bidAmountText;
        [SerializeField] private TextMeshProUGUI _currentBalanceText;
        [SerializeField] private Button _cancelBidButton;
        [SerializeField] private Button _confirmBidButton;

        private readonly List<OfficeSlotView> _slotViews = new();
        private readonly CompositeDisposable _viewSubscriptions = new();
        private readonly UnityAction<float> _sliderAction;
        private readonly UnityAction _cancelBidAction;
        private readonly UnityAction _confirmBidAction;
        private OfficeViewModel _viewModel;
        private bool _listenersBound;

        public OfficeView() {
            _sliderAction = OnBidSliderChanged;
            _cancelBidAction = CancelBidPopup;
            _confirmBidAction = ConfirmBidPopup;
        }

        private void Awake() {
            if (_bidPopupRoot != null) _bidPopupRoot.SetActive(false);
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
            } catch (OperationCanceledException) {
                return;
            }

            if (this == null) return;
            _viewModel = new OfficeViewModel(locator.Get<OfficeService>(), locator.Get<WalletService>());
            _viewModel.SummaryChanged.Subscribe(_ => RefreshSummary()).AddTo(_viewSubscriptions);
            _viewModel.SlotsChanged.Subscribe(_ => RefreshSlots()).AddTo(_viewSubscriptions);
            _viewModel.BidChanged.Subscribe(_ => RefreshBidPopup()).AddTo(_viewSubscriptions);
            _pullTab.OpenState.Subscribe(OnPullTabOpenChanged).AddTo(_viewSubscriptions);
            _pullTab.BindAvailability(_viewModel.Availability);
            BindPopupListeners();
            RefreshSummary();
            RefreshSlots();
            RefreshBidPopup();
        }

        private void OnRectTransformDimensionsChange() {
            if (_slotViewport != null && _slotGrid != null) UpdateGridLayout();
        }

        private void RefreshSummary() {
            if (_viewModel == null) return;
            _staffText.text = $"Staff: {_viewModel.ClerkCount}/{_viewModel.Capacity}";
            _pendingText.text = $"Pending: {_viewModel.PendingHireCount} hires · {_viewModel.PendingSalaryReviewCount} reviews";
            _speedText.text = $"Speed: {_viewModel.DocumentsPerSecondPerClerk.ToString("0.##", CultureInfo.InvariantCulture)}/s per clerk";
            _qualityText.text = $"Quality ceiling: {FormatPercent(_viewModel.QualityCeiling)}";
            _acceptanceText.text = $"Acceptance: {FormatPercent(_viewModel.AcceptanceThreshold)}";
            _rewardText.text = $"Office reward: {FormatPercent(_viewModel.RewardMultiplier)}";
        }

        private void RefreshSlots() {
            if (_viewModel == null) return;
            _tooltip.Hide();

            bool hasPurchase = false;
            IReadOnlyList<OfficeSlotPresentationModel> models = _viewModel.Slots;
            for (int index = 0; index < models.Count; index++) {
                if (models[index].State == OfficeSlotState.Purchase) {
                    hasPurchase = true;
                    break;
                }
            }

            if (!hasPurchase && _bidPopupRoot.activeSelf) CancelBidPopup();
            ClearSlotViews();
            for (int index = 0; index < models.Count; index++) {
                OfficeSlotView slot = Instantiate(_slotPrefab, _slotContent, false);
                slot.gameObject.SetActive(true);
                slot.Bind(
                    models[index],
                    ReviewSalary,
                    Dismiss,
                    ShowBidPopup,
                    Hire,
                    ShowReviewTooltip,
                    _tooltip.Hide);
                _slotViews.Add(slot);
            }

            UpdateGridLayout();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_slotContent);
        }

        private void UpdateGridLayout() {
            int horizontalPadding = _slotGrid.padding.left + _slotGrid.padding.right;
            float width = Mathf.Max(1f, _slotContent.rect.width - horizontalPadding);
            float spacing = Mathf.Max(0f, _slotGrid.spacing.x);
            int columns = Mathf.Max(1, Mathf.FloorToInt((width + spacing) / (_minimumSlotWidth + spacing)));
            float cellWidth = Mathf.Max(1f, (width - spacing * (columns - 1)) / columns);
            _slotGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _slotGrid.constraintCount = columns;
            _slotGrid.cellSize = new Vector2(cellWidth, _slotHeight);
        }

        private void ShowReviewTooltip(
            OfficeSlotPresentationModel model,
            Vector2 screenPosition,
            Camera eventCamera) {
            string status = model.IsReviewPending
                ? "A salary review is already awaiting a signature."
                : "Review the clerk's salary to replace their bonus efficiency after a successful signature.";
            _tooltip.Show($"{status}\nCost: {model.ReviewCost}$", screenPosition, eventCamera);
        }

        private void ShowBidPopup() {
            if (_viewModel == null || !_viewModel.BeginBidEdit()) return;
            _bidPopupRoot.SetActive(true);
            _bidPopupRoot.transform.SetAsLastSibling();
            RefreshBidPopup();
        }

        private void RefreshBidPopup() {
            if (_viewModel == null || !_bidPopupRoot.activeSelf) return;
            _bidSlider.SetValueWithoutNotify(_viewModel.BidSliderValue);
            _bidSlider.interactable = !_viewModel.PreviewBid.IsZero || _viewModel.BidSliderValue > 0f;
            _bidAmountText.text = $"Bid: {_viewModel.PreviewBid}$";
            _currentBalanceText.text = $"Balance: {_viewModel.CurrentBalance}$";
            _confirmBidButton.interactable = _viewModel.CanConfirmBid;
        }

        private void OnBidSliderChanged(float value) {
            _viewModel?.SetBidSliderValue(value);
        }

        private void CancelBidPopup() {
            _viewModel?.CancelBidEdit();
            if (_bidPopupRoot != null) _bidPopupRoot.SetActive(false);
        }

        private void ConfirmBidPopup() {
            if (_viewModel == null || !_viewModel.ConfirmBidEdit()) return;
            _bidPopupRoot.SetActive(false);
        }

        private void ReviewSalary(int clerkId) {
            _viewModel?.TryReviewSalary(clerkId);
        }

        private void Dismiss(int clerkId) {
            _viewModel?.TryDismiss(clerkId);
        }

        private void Hire() {
            _viewModel?.TryHire();
        }

        private void OnPullTabOpenChanged(bool isOpen) {
            if (isOpen) return;
            CancelBidPopup();
            _tooltip.Hide();
        }

        private void BindPopupListeners() {
            if (_listenersBound) return;
            _listenersBound = true;
            _bidSlider.onValueChanged.AddListener(_sliderAction);
            _cancelBidButton.onClick.AddListener(_cancelBidAction);
            _confirmBidButton.onClick.AddListener(_confirmBidAction);
        }

        private void UnbindPopupListeners() {
            if (!_listenersBound) return;
            _listenersBound = false;
            _bidSlider.onValueChanged.RemoveListener(_sliderAction);
            _cancelBidButton.onClick.RemoveListener(_cancelBidAction);
            _confirmBidButton.onClick.RemoveListener(_confirmBidAction);
        }

        private void ClearSlotViews() {
            _tooltip.Hide();
            for (int index = 0; index < _slotViews.Count; index++) {
                OfficeSlotView slot = _slotViews[index];
                if (slot == null) continue;
                slot.Unbind();
                Destroy(slot.gameObject);
            }

            _slotViews.Clear();
        }

        private void OnDestroy() {
            CancelBidPopup();
            _pullTab?.UnbindAvailability();
            _viewSubscriptions.Dispose();
            UnbindPopupListeners();
            ClearSlotViews();
            _viewModel?.Dispose();
            _viewModel = null;
        }

        private bool ValidateReferences() {
            bool valid = _pullTab != null && _staffText != null && _pendingText != null && _speedText != null &&
                         _qualityText != null && _acceptanceText != null && _rewardText != null &&
                         _slotViewport != null && _slotContent != null && _slotGrid != null &&
                         _slotPrefab != null && _tooltip != null && _bidPopupRoot != null &&
                         _bidSlider != null && _bidAmountText != null && _currentBalanceText != null &&
                         _cancelBidButton != null &&
                         _confirmBidButton != null;
            if (valid) return true;
            Debug.LogError("OfficeView requires all pull tab, summary, slot, tooltip, and bid popup references.", this);
            return false;
        }

        private static string FormatPercent(float value) {
            return $"{(value * 100f).ToString("0.#", CultureInfo.InvariantCulture)}%";
        }
    }
}
