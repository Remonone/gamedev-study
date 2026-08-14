using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Presentation {
    public sealed class MetaPurchaseConfirmationView : MonoBehaviour {
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private TextMeshProUGUI _message;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;

        private readonly UnityAction _confirmAction;
        private readonly UnityAction _cancelAction;
        private Action _onConfirm;

        public MetaPurchaseConfirmationView() {
            _confirmAction = Confirm;
            _cancelAction = Hide;
        }

        private void Awake() {
            _confirmButton?.onClick.AddListener(_confirmAction);
            _cancelButton?.onClick.AddListener(_cancelAction);
            Hide();
        }

        public void Show(UpgradeNodePresentationModel node, Action onConfirm) {
            if (node == null) throw new ArgumentNullException(nameof(node));
            _onConfirm = onConfirm ?? throw new ArgumentNullException(nameof(onConfirm));
            if (_message != null) {
                _message.text = $"Buy {node.Name} for {node.Price} meta points?\n" +
                                "Money, ordinary upgrades, office, bills, bank and archive research will reset.";
            }
            if (_panelRoot != null) _panelRoot.SetActive(true);
        }

        public void Hide() {
            _onConfirm = null;
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }

        private void Confirm() {
            Action action = _onConfirm;
            Hide();
            action?.Invoke();
        }

        private void OnDestroy() {
            _confirmButton?.onClick.RemoveListener(_confirmAction);
            _cancelButton?.onClick.RemoveListener(_cancelAction);
            _onConfirm = null;
        }
    }
}
