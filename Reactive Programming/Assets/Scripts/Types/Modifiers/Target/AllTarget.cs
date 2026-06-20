using Types.Modifiers.Definitions.Buildings;
using UnityEngine;

namespace Types.Modifiers.Definitions.Target {
    [CreateAssetMenu(fileName = "TypeTarget", menuName = "Clicker/Modifiers/Target/All", order = 0)]
    public class AllTarget : ModifierTarget {
        public override bool Matches(BuildingState building) {
            return true;
        }
    }
}