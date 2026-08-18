using TMPro;
using UnityEngine;

namespace Presentation {
    public sealed class StatisticRowView : MonoBehaviour {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private TextMeshProUGUI _value;

        public string StatisticId { get; private set; }

        public bool HasRequiredReferences => _label != null && _value != null;

        public void Bind(StatisticRowPresentationModel model) {
            if (!HasRequiredReferences) {
                Debug.LogError("StatisticRowView is missing required text references.", this);
                return;
            }

            StatisticId = model.StatisticId;
            _label.text = model.Label;
            _value.text = model.Value;
        }

        public void Refresh(StatisticRowPresentationModel model) {
            if (!HasRequiredReferences) return;
            _value.text = model.Value;
        }
    }
}
