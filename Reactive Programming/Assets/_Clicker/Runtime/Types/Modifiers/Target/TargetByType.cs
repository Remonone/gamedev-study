using Types.Buildings;
using Types.Enums;
using UnityEngine;

namespace Types.Modifiers.Target {
    [CreateAssetMenu(fileName = "TypeTarget", menuName = "Clicker/Modifiers/Target/By Type", order = 0)]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp")]
    public class TargetByType : ModifierTarget {
        
        [Tooltip("Building type that this target matches.")]
        public GovernmentInteractionType Type;
        
        public override bool Matches(BuildingState building) {
            return building.Definition.Type == Type;
        }
    }
}
