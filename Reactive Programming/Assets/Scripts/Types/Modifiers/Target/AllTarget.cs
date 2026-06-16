using Types.Enums.Buildings;
using UnityEngine;

namespace Types.Enums.Target {
    [CreateAssetMenu(fileName = "TypeTarget", menuName = "Clicker/Modifiers/Target/All", order = 0)]
    public class AllTarget : ModifierTarget {
        public override bool Matches(BuildingState building) {
            return true;
        }
    }
}