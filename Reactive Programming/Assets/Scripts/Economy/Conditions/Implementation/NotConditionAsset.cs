using Types.Buildings;
using Types.Modifiers;
using UnityEngine;

namespace Economy.Conditions.Implementation {
    [CreateAssetMenu(fileName = "Not Condition Asset", menuName = "Clicker/Conditions/Not", order = 0)]
    public class NotConditionAsset : ConditionAsset {
        [SerializeField, Tooltip("Condition to invert. If empty, this condition passes.")]
        private ConditionAsset _condition;
        public override bool Evaluate(ISessionContext context, BuildingState building) {
            if (_condition == null) return true;
            return !_condition.Evaluate(context, building);
        }
    }
}
