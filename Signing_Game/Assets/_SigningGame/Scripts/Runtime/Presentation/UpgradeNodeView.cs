using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Presentation {
    [RequireComponent(typeof(RectTransform), typeof(Button), typeof(CanvasGroup))]
    public sealed class UpgradeNodeView : MonoBehaviour {
        [SerializeField] private Button _button;
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private GameObject _lockedOverlay;
        [SerializeField, Range(0f, 1f)] private float _lockedAlpha = 0.55f;

        private CanvasGroup _canvasGroup;
        private UnityAction _clickAction;

        private void Awake() {
            _button ??= GetComponent<Button>();
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        public void Bind(UpgradeNodePresentationModel model, Action<string> onSelected) {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (onSelected == null) throw new ArgumentNullException(nameof(onSelected));

            Unbind();
            if (_icon != null) {
                _icon.sprite = model.Icon;
                _icon.preserveAspect = true;
            }

            if (_nameText != null) _nameText.text = model.Name;
            if (_levelText != null) _levelText.text = model.LevelText;
            if (_lockedOverlay != null) _lockedOverlay.SetActive(!model.IsUnlocked);
            _canvasGroup.alpha = model.IsUnlocked ? 1f : _lockedAlpha;

            _clickAction = () => onSelected(model.Id);
            _button.onClick.AddListener(_clickAction);
        }

        public void Unbind() {
            if (_clickAction == null || _button == null) return;
            _button.onClick.RemoveListener(_clickAction);
            _clickAction = null;
        }

        private void OnDestroy() {
            Unbind();
        }
    }
}
