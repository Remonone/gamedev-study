using Types.Enums.Buildings;
using UnityEngine;

namespace Types.Enums.Target {
    
    [CreateAssetMenu(fileName = "TypeTarget", menuName = "Clicker/Modifiers/Target/By Id", order = 0)]
    public class TargetById : ModifierTarget {

        [Tooltip("BuildingDefinition.Name that must match exactly.")]
        public string Name;
        
        public override bool Matches(BuildingState building) {
            return building.Definition.Name == Name;
        }
    }
}
