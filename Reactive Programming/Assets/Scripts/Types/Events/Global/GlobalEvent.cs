using System.Collections.Generic;
using UnityEngine;

namespace Types.Events.Global {
    [CreateAssetMenu(fileName = "Global Event", menuName = "Clicker/Events/Global Event", order = 0)]
    public class GlobalEvent : ScriptableObject {
        [SerializeField, Tooltip("Stable id for saves/debug output. Keep unique among global events.")]
        private string _id;
        [SerializeField, Tooltip("Display name shown in the active event tooltip.")]
        private string _eventName;
        [SerializeField, TextArea(2, 5), Tooltip("Description shown in the active event tooltip.")]
        private string _description;
        [SerializeField, Tooltip("Icon shown in the active event indicator.")]
        private Sprite _icon;
        [SerializeField, Min(0f), Tooltip("How long this event remains active, in seconds.")]
        private float _durationSeconds = 60f;
        [SerializeField, Tooltip("Effects applied while this event is active.")]
        private List<GlobalEffect> _effects = new();

        public string Id => _id;
        public string EventName => _eventName;
        public string Description => _description;
        public Sprite Icon => _icon;
        public float DurationSeconds => Mathf.Max(0f, _durationSeconds);
        public IReadOnlyList<GlobalEffect> Effects => _effects;

        private void OnValidate() {
            if (_durationSeconds < 0f) {
                _durationSeconds = 0f;
            }
        }
    }
}
