using Types.Buildings;
using UnityEngine;

namespace Types.Economy.Modifiers.Target {
    
    [CreateAssetMenu(fileName = "TypeTarget", menuName = "Clicker/Modifiers/Target By Type", order = 0)]
    public class TargetById : ModifierTarget {

        public string Name;
        
        public override bool Matches(BuildingState building) {
            return building.Definition.Name == Name;
        }
    }
}