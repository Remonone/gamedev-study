using Types.Values;
using UnityEngine;

namespace Types.Research {
    [CreateAssetMenu(fileName = "ResearchConfig", menuName = "Clicker/Research/Research Config", order = 0)]
    public class ResearchConfig : ScriptableObject {
        [SerializeField, Tooltip("Base amount of research points required before per-research and scale multipliers.")]
        private Value _baseResearchCost = new(100d);

        [SerializeField, Min(0.0001f), Tooltip("Linear cost multiplier applied to completed research count + 1.")]
        private float _costPerResearchMultiplier = 1f;

        [SerializeField, Min(0f), Tooltip("Research points per second produced by one point of Archive influence.")]
        private float _archiveInfluenceToPointsPerSecond = 1f;

        [SerializeField, Min(0.0001f), Tooltip("Practice-driven scale modifier. Currently expected to stay at 1.")]
        private float _scaleModifier = 1f;

        [SerializeField]
        private string _readyNotificationTitle = "Research complete";

        [SerializeField]
        private string _readyNotificationMessage = "A research iteration is ready to claim.";

        public Value BaseResearchCost => _baseResearchCost > Value.Zero ? _baseResearchCost : Value.One;
        public float CostPerResearchMultiplier => Mathf.Max(0.0001f, _costPerResearchMultiplier);
        public float ArchiveInfluenceToPointsPerSecond => Mathf.Max(0f, _archiveInfluenceToPointsPerSecond);
        public float ScaleModifier => Mathf.Max(0.0001f, _scaleModifier);
        public string ReadyNotificationTitle => string.IsNullOrWhiteSpace(_readyNotificationTitle) ? "Research complete" : _readyNotificationTitle;
        public string ReadyNotificationMessage => string.IsNullOrWhiteSpace(_readyNotificationMessage) ? "A research iteration is ready to claim." : _readyNotificationMessage;
    }
}
