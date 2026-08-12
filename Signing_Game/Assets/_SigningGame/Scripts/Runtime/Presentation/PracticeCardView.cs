using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Presentation {
    public sealed class PracticeCardView : MonoBehaviour {
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _title;
        [SerializeField] private TextMeshProUGUI _description;
        [SerializeField] private TextMeshProUGUI _detail;
        [SerializeField] private Button _actionButton;
        [SerializeField] private TextMeshProUGUI _actionLabel;

        private UnityAction _action;

        public void BindOffer(ResearchOfferPresentationModel model, Action<string> select) {
            if (model == null) throw new ArgumentNullException(nameof(model));
            Unbind();
            Apply(model.Title, model.Description, model.Rarity, model.Icon);
            if (_detail != null) _detail.color = model.RarityColor;
            if (_actionLabel != null) _actionLabel.text = "Select";
            if (_actionButton != null) {
                _actionButton.gameObject.SetActive(true);
                _action = () => select?.Invoke(model.Id);
                _actionButton.onClick.AddListener(_action);
            }
        }

        public void BindActive(ActivePracticePresentationModel model) {
            if (model == null) throw new ArgumentNullException(nameof(model));
            Unbind();
            Apply(model.Title, model.Description, model.Duration, model.Icon);
            if (_actionButton != null) _actionButton.gameObject.SetActive(false);
        }

        public void Unbind() {
            if (_actionButton != null && _action != null) _actionButton.onClick.RemoveListener(_action);
            _action = null;
        }

        private void OnDestroy() => Unbind();

        private void Apply(string title, string description, string detail, Sprite icon) {
            if (_title != null) _title.text = title ?? string.Empty;
            if (_description != null) _description.text = description ?? string.Empty;
            if (_detail != null) _detail.text = detail ?? string.Empty;
            if (_icon != null) {
                _icon.sprite = icon;
                _icon.gameObject.SetActive(icon != null);
            }
        }
    }
}
