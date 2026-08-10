using System;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation {
    public sealed class BillRequirementView : MonoBehaviour {
        [SerializeField] private Image _colorSquare;
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private PointerTooltipTrigger _tooltipTrigger;

        public void Bind(
            BillRequirementPresentationModel model,
            Action<string, Vector2, Camera> showTooltip,
            Action hideTooltip) {
            Unbind();
            _colorSquare.color = model.Color;
            _label.text = model.Label;
            _canvasGroup.alpha = model.IsSatisfied ? 1f : 0.5f;
            _tooltipTrigger.Bind(
                (position, camera) => showTooltip(model.Tooltip, position, camera),
                hideTooltip);
        }

        public void Unbind() => _tooltipTrigger?.Unbind();
        private void OnDestroy() => Unbind();
    }
}
