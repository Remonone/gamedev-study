using Types.Modifiers.Definitions.Buildings;
using UnityEngine;

namespace Types.Modifiers.Definitions.Target {
    public abstract class ModifierTarget : ScriptableObject {
        public abstract bool Matches(BuildingState building);
    }
}