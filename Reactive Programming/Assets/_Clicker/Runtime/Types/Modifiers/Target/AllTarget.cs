using Types.Buildings;
using UnityEngine;

namespace Types.Modifiers.Target {
    [CreateAssetMenu(fileName = "TypeTarget", menuName = "Clicker/Modifiers/Target/All", order = 0)]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp")]
    public class AllTarget : ModifierTarget {
        public override bool Matches(BuildingState building) {
            return true;
        }
    }
}
