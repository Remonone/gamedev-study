using Types.Buildings;
using Types.Modifiers;
using UnityEngine;

namespace Economy.Conditions.Implementation {
    [CreateAssetMenu(fileName = "Or Condition Asset", menuName = "Clicker/Conditions/Or", order = 0)]
    public class OrConditionAsset : ConditionAsset {
        [SerializeField, Tooltip("At least one listed condition must pass. Empty list means this condition passes.")]
        private ConditionAsset[] _conditions;
        public override bool Evaluate(ISessionContext context, BuildingState building) {
            if (_conditions == null || _conditions.Length == 0) return true;
            foreach (var condition in _conditions) {
                if (condition.Evaluate(context, building)) return true;
            }

            return false;
        }
    }
}
