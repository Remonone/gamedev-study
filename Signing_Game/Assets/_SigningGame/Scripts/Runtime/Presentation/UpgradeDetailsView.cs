using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
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
        private readonly List<RaycastResult> _raycastResults = new();
        private UnityAction _buyAction;
        private Action _dismissAction;

        private void Awake() {
            Hide();
        }

        private void Update() {
            if (_panelRoot == null || !_panelRoot.activeInHierarchy || Pointer.current == null ||
                !Pointer.current.press.wasPressedThisFrame) return;

            Vector2 pointerPosition = Pointer.current.position.ReadValue();
            if (IsPointerInsideDetails(pointerPosition) || IsPointerOverUpgrade(pointerPosition)) return;

            Dismiss();
        }

        public void Show(UpgradeNodePresentationModel model, Func<string, bool> purchase, Action dismiss) {
            if (model == null) {
                Hide();
                return;
            }
            if (purchase == null) throw new ArgumentNullException(nameof(purchase));
            if (dismiss == null) throw new ArgumentNullException(nameof(dismiss));

            if (_nameText != null) _nameText.text = model.Name;
            if (_icon != null) {
                _icon.sprite = model.Icon;
                _icon.preserveAspect = true;
            }

            if (_descriptionText != null) _descriptionText.text = model.Description;
            if (_levelText != null) _levelText.text = model.LevelText;
            if (_priceText != null) _priceText.text = model.Price;
            if (_buyButton != null) {
                UnbindBuyButton();
                _buyAction = () => purchase(model.Id);
                _buyButton.onClick.AddListener(_buyAction);
                _buyButton.interactable = model.CanPurchase;
                _buyButton.gameObject.SetActive(true);
            }
            _dismissAction = dismiss;
            if (_panelRoot != null) _panelRoot.SetActive(true);
        }

        public void Hide() {
            UnbindBuyButton();
            _dismissAction = null;
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }

        private void Dismiss() {
            Action dismiss = _dismissAction;
            Hide();
            dismiss?.Invoke();
        }

        private void OnDestroy() {
            UnbindBuyButton();
        }

        private bool IsPointerInsideDetails(Vector2 pointerPosition) {
            if (_panelRoot.transform is not RectTransform panelRect) return false;
            Canvas canvas = panelRect.GetComponentInParent<Canvas>();
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            return RectTransformUtility.RectangleContainsScreenPoint(panelRect, pointerPosition, eventCamera);
        }

        private bool IsPointerOverUpgrade(Vector2 pointerPosition) {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            _raycastResults.Clear();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = pointerPosition }, _raycastResults);
            for (int index = 0; index < _raycastResults.Count; index++) {
                if (_raycastResults[index].gameObject.GetComponentInParent<UpgradeNodeView>() != null) return true;
            }

            return false;
        }

        private void UnbindBuyButton() {
            if (_buyButton == null || _buyAction == null) return;
            _buyButton.onClick.RemoveListener(_buyAction);
            _buyAction = null;
        }
    }
}
