using Types.Buildings;
using JetBrains.Annotations;
using Types.Modifiers;
using UnityEngine;

namespace Economy.Conditions.Implementation {
    [CreateAssetMenu(fileName = "And Condition Asset", menuName = "Clicker/Conditions/And", order = 0)]
    public class AndConditionAsset : ConditionAsset {
        [SerializeField, CanBeNull, Tooltip("All listed conditions must pass. Empty list means this condition passes.")]
        private ConditionAsset[] _conditions;
        public override bool Evaluate(ISessionContext context, BuildingState building) {
            if (_conditions == null || _conditions.Length == 0) return true;
            foreach (var condition in _conditions) {
                if (!condition.Evaluate(context, building)) return false;
            }

            return true;
        }
    }
}
