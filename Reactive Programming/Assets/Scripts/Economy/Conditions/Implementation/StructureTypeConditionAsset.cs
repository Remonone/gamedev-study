using Types.Enums.Buildings;
using Types.Enums;
using UnityEngine;

namespace Economy.Conditions.Implementation {
    [CreateAssetMenu(fileName = "Condition_StructureType", menuName = "Clicker/Economy/Conditions/Structure Type")]
    public sealed class StructureTypeConditionAsset : ConditionAsset {
        [SerializeField, Tooltip("Building type that must match for this condition to pass.")]
        private GovernmentInteractionType _type;

        public override bool Evaluate(ISessionContext context, BuildingState building) {
            return building.Definition.Type == _type;
        }
    }
}
