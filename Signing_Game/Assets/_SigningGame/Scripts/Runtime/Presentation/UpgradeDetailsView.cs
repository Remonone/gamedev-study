using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation {
    public sealed class UpgradeDetailsView : MonoBehaviour {
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private TextMeshProUGUI _priceText;
        [SerializeField] private Button _buyButton;

        private void Awake() {
            Hide();
        }

        public void Show(UpgradeNodePresentationModel model) {
            if (model == null) {
                Hide();
                return;
            }

            if (_nameText != null) _nameText.text = model.Name;
            if (_icon != null) {
                _icon.sprite = model.Icon;
                _icon.preserveAspect = true;
            }

            if (_descriptionText != null) _descriptionText.text = model.Description;
            if (_levelText != null) _levelText.text = $"{model.CurrentLevel}/{model.MaxLevel}";
            if (_priceText != null) _priceText.text = model.Price;
            if (_buyButton != null) _buyButton.gameObject.SetActive(true);
            if (_panelRoot != null) _panelRoot.SetActive(true);
        }

        public void Hide() {
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }
    }
}
