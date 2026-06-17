using Types.Enums.Buildings;
using JetBrains.Annotations;
using Types.Enums;
using UnityEngine;

namespace Economy.Conditions.Implementation {
    [CreateAssetMenu(fileName = "Or Condition Asset", menuName = "Clicker/Conditions/Or", order = 0)]
    public class OrConditionAsset : ConditionAsset {
        [SerializeField] private ConditionAsset[] _conditions;
        public override bool Evaluate(ISessionContext context, BuildingState building) {
            if (_conditions == null || _conditions.Length == 0) return true;
            foreach (var condition in _conditions) {
                if (condition.Evaluate(context, building)) return true;
            }

            return false;
        }
    }
}