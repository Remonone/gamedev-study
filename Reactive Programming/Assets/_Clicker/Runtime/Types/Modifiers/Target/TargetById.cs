using Types.Buildings;
using UnityEngine;

namespace Types.Modifiers.Target {
    
    [CreateAssetMenu(fileName = "TypeTarget", menuName = "Clicker/Modifiers/Target/By Id", order = 0)]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp")]
    public class TargetById : ModifierTarget {

        [Tooltip("BuildingDefinition.Name that must match exactly.")]
        public string Name;
        
        public override bool Matches(BuildingState building) {
            return building.Definition.Name == Name;
        }
    }
}
