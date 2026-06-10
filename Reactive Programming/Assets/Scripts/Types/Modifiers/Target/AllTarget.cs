using Types.Buildings;
using UnityEngine;

namespace Types.Economy.Modifiers.Target {
    [CreateAssetMenu(fileName = "TypeTarget", menuName = "Clicker/Modifiers/All Targets", order = 0)]
    public class AllTarget : ModifierTarget {
        public override bool Matches(BuildingState building) {
            return true;
        }
    }
}