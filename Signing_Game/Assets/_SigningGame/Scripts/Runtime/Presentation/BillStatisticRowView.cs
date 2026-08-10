using System;
using TMPro;
using UI;
using UnityEngine;

namespace Presentation {
    public sealed class BillStatisticRowView : MonoBehaviour {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private TextMeshProUGUI _value;
        [SerializeField] private PointerTooltipTrigger _tooltipTrigger;

        public void Bind(
            BillStatisticPresentationModel model,
            Action<string, Vector2, Camera> showTooltip,
            Action hideTooltip) {
            _label.text = model.Label;
            _value.text = model.Value;
            _tooltipTrigger?.Bind(
                (position, camera) => showTooltip(model.Tooltip, position, camera),
                hideTooltip);
        }

        public void Unbind() => _tooltipTrigger?.Unbind();
        private void OnDestroy() => Unbind();
    }
}
