using Types.Modifiers.Definitions.Buildings;
using UnityEngine;

namespace Types.Modifiers.Definitions.Target {
    [CreateAssetMenu(fileName = "TypeTarget", menuName = "Clicker/Modifiers/Target/By Type", order = 0)]
    public class TargetByType : ModifierTarget {
        
        [Tooltip("Building type that this target matches.")]
        public GovernmentInteractionType Type;
        
        public override bool Matches(BuildingState building) {
            return building.Definition.Type == Type;
        }
    }
}
