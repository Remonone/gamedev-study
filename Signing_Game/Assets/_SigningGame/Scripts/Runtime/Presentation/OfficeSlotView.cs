using System;
using System.Globalization;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Presentation {
    public sealed class OfficeSlotView : MonoBehaviour {
        [Header("States")]
        [SerializeField] private GameObject _clerkRoot;
        [SerializeField] private GameObject _purchaseRoot;
        [SerializeField] private GameObject _vacantRoot;

        [Header("Clerk")]
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _ageText;
        [SerializeField] private TextMeshProUGUI _baseEfficiencyText;
        [SerializeField] private TextMeshProUGUI _bonusEfficiencyText;
        [SerializeField] private Button _reviewButton;
        [SerializeField] private Button _fireButton;
        [SerializeField] private PointerTooltipTrigger _reviewTooltipTrigger;

        [Header("Purchase")]
        [SerializeField] private TextMeshProUGUI _bidText;
        [SerializeField] private Button _changeBidButton;
        [SerializeField] private Button _hireButton;

        private UnityAction _reviewAction;
        private UnityAction _fireAction;
        private UnityAction _changeBidAction;
        private UnityAction _hireAction;

        public void Bind(
            OfficeSlotPresentationModel model,
            Action<int> reviewSalary,
            Action<int> dismiss,
            Action changeBid,
            Action hire,
            Action<OfficeSlotPresentationModel, Vector2, Camera> showTooltip,
            Action hideTooltip) {
            if (model == null) throw new ArgumentNullException(nameof(model));
            Unbind();

            _clerkRoot.SetActive(model.State == OfficeSlotState.Clerk);
            _purchaseRoot.SetActive(model.State == OfficeSlotState.Purchase);
            _vacantRoot.SetActive(model.State == OfficeSlotState.Vacant);

            if (model.State == OfficeSlotState.Clerk) {
                _nameText.text = model.Name;
                _ageText.text = $"Age: {model.Age}";
                _baseEfficiencyText.text = $"Base Efficiency: {FormatMultiplier(model.BaseEfficiency)}";
                _bonusEfficiencyText.text = $"Bonus Efficiency: {FormatPercent(model.BonusEfficiency)}";
                _reviewButton.interactable = model.CanReview;
                _fireButton.interactable = true;
                _reviewAction = () => reviewSalary(model.ClerkId);
                _fireAction = () => dismiss(model.ClerkId);
                _reviewButton.onClick.AddListener(_reviewAction);
                _fireButton.onClick.AddListener(_fireAction);
                _reviewTooltipTrigger.Bind(
                    (position, camera) => showTooltip(model, position, camera),
                    hideTooltip);
            }
            else if (model.State == OfficeSlotState.Purchase) {
                _bidText.text = $"Bid\n{model.Bid}$";
                _hireButton.interactable = model.CanHire;
                _changeBidAction = () => changeBid();
                _hireAction = () => hire();
                _changeBidButton.onClick.AddListener(_changeBidAction);
                _hireButton.onClick.AddListener(_hireAction);
            }
        }

        public void Unbind() {
            _reviewTooltipTrigger?.Unbind();
            if (_reviewAction != null && _reviewButton != null) _reviewButton.onClick.RemoveListener(_reviewAction);
            if (_fireAction != null && _fireButton != null) _fireButton.onClick.RemoveListener(_fireAction);
            if (_changeBidAction != null && _changeBidButton != null) {
                _changeBidButton.onClick.RemoveListener(_changeBidAction);
            }

            if (_hireAction != null && _hireButton != null) _hireButton.onClick.RemoveListener(_hireAction);
            _reviewAction = null;
            _fireAction = null;
            _changeBidAction = null;
            _hireAction = null;
        }

        private void OnDestroy() {
            Unbind();
        }

        private static string FormatMultiplier(double value) {
            return value >= double.MaxValue ? "∞x" : $"{value.ToString("0.##", CultureInfo.InvariantCulture)}x";
        }

        private static string FormatPercent(double value) {
            if (value >= double.MaxValue / 100d) return "+∞%";
            return $"+{(value * 100d).ToString("0.#", CultureInfo.InvariantCulture)}%";
        }
    }
}
