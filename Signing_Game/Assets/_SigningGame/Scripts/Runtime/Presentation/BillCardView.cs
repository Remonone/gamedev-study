using System;
using System.Collections.Generic;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Presentation {
    public sealed class BillCardView : MonoBehaviour {
        [Header("Common")]
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _title;
        [SerializeField] private TextMeshProUGUI _description;

        [Header("Catalog")]
        [SerializeField] private GameObject _catalogFooter;
        [SerializeField] private RectTransform _requirementsRoot;
        [SerializeField] private BillRequirementView _requirementPrefab;
        [SerializeField] private TextMeshProUGUI _price;
        [SerializeField] private Button _buyButton;
        [SerializeField] private PointerTooltipTrigger _buyTooltip;

        [Header("Active")]
        [SerializeField] private GameObject _activeFooter;
        [SerializeField] private Slider _progress;
        [SerializeField] private TextMeshProUGUI _progressText;
        [SerializeField] private Slider _priority;
        [SerializeField] private TextMeshProUGUI _priorityText;

        [Header("Completed")]
        [SerializeField] private GameObject _completedFooter;
        [SerializeField] private Button _expandButton;
        [SerializeField] private TextMeshProUGUI _expandLabel;
        [SerializeField] private GameObject _statisticsRoot;
        [SerializeField] private RectTransform _statisticsContent;
        [SerializeField] private BillStatisticRowView _statisticRowPrefab;

        private readonly List<BillRequirementView> _requirements = new();
        private readonly List<BillStatisticRowView> _statistics = new();
        private readonly UnityAction _buyAction;
        private readonly UnityAction _expandAction;
        private readonly UnityAction<float> _priorityAction;
        private Action<long> _purchase;
        private Action<long> _toggle;
        private Action<long, int> _setPriority;
        private long _id;
        private bool _bound;

        public BillCardView() {
            _buyAction = OnBuy;
            _expandAction = OnExpand;
            _priorityAction = OnPriority;
        }

        public void Bind(
            BillCardPresentationModel model,
            Action<long> purchase,
            Action<long> toggle,
            Action<long, int> setPriority,
            Action<string, Vector2, Camera> showTooltip,
            Action hideTooltip) {
            Unbind();
            _id = model.Id;
            _purchase = purchase;
            _toggle = toggle;
            _setPriority = setPriority;
            _icon.sprite = model.Icon;
            _icon.enabled = model.Icon != null;
            _title.text = model.Title;
            _description.text = model.Description;

            bool catalog = model.Kind == BillCardKind.Catalog;
            bool active = model.Kind == BillCardKind.Active;
            bool completed = model.Kind == BillCardKind.Completed;
            _catalogFooter.SetActive(catalog);
            _activeFooter.SetActive(active);
            _completedFooter.SetActive(completed);

            if (catalog) {
                _price.text = $"{model.Price}$";
                _buyButton.interactable = model.CanPurchase;
                for (int index = 0; index < model.Requirements.Count; index++) {
                    BillRequirementView requirement = Instantiate(_requirementPrefab, _requirementsRoot, false);
                    requirement.gameObject.SetActive(true);
                    requirement.Bind(model.Requirements[index], showTooltip, hideTooltip);
                    _requirements.Add(requirement);
                }
                if (!model.CanPurchase && !string.IsNullOrWhiteSpace(model.PurchaseBlockerTooltip)) {
                    _buyTooltip.Bind(
                        (position, camera) => showTooltip(model.PurchaseBlockerTooltip, position, camera),
                        hideTooltip);
                }
            }
            if (active) {
                _progress.SetValueWithoutNotify(model.Progress);
                _progressText.text = model.ProgressText;
                _priority.minValue = 1f;
                _priority.maxValue = Mathf.Max(1, model.MaximumPriority);
                _priority.wholeNumbers = true;
                _priority.SetValueWithoutNotify(model.Priority);
                _priorityText.text = $"Priority: {model.Priority}";
            }
            if (completed) {
                _expandLabel.text = model.IsExpanded ? "Hide details" : "Show details";
                _statisticsRoot.SetActive(model.IsExpanded);
                if (model.IsExpanded) {
                    for (int index = 0; index < model.Statistics.Count; index++) {
                        BillStatisticRowView row = Instantiate(_statisticRowPrefab, _statisticsContent, false);
                        row.gameObject.SetActive(true);
                        row.Bind(model.Statistics[index], showTooltip, hideTooltip);
                        _statistics.Add(row);
                    }
                }
            }

            _buyButton.onClick.AddListener(_buyAction);
            _expandButton.onClick.AddListener(_expandAction);
            _priority.onValueChanged.AddListener(_priorityAction);
            _bound = true;
        }

        public void Unbind() {
            if (_bound) {
                _buyButton.onClick.RemoveListener(_buyAction);
                _expandButton.onClick.RemoveListener(_expandAction);
                _priority.onValueChanged.RemoveListener(_priorityAction);
            }
            _bound = false;
            _buyTooltip?.Unbind();
            for (int index = 0; index < _requirements.Count; index++) {
                if (_requirements[index] == null) continue;
                _requirements[index].Unbind();
                Destroy(_requirements[index].gameObject);
            }
            for (int index = 0; index < _statistics.Count; index++) {
                if (_statistics[index] == null) continue;
                _statistics[index].Unbind();
                Destroy(_statistics[index].gameObject);
            }
            _requirements.Clear();
            _statistics.Clear();
            _purchase = null;
            _toggle = null;
            _setPriority = null;
        }

        private void OnBuy() => _purchase?.Invoke(_id);
        private void OnExpand() => _toggle?.Invoke(_id);
        private void OnPriority(float value) {
            int priority = Mathf.Max(1, Mathf.RoundToInt(value));
            _priorityText.text = $"Priority: {priority}";
            _setPriority?.Invoke(_id, priority);
        }
        private void OnDestroy() => Unbind();
    }
}
