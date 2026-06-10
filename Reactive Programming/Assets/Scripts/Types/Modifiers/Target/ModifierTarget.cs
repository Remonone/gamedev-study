using Types.Buildings;
using UnityEngine;

namespace Types.Economy.Modifiers.Target {
    public abstract class ModifierTarget : ScriptableObject {
        public abstract bool Matches(BuildingState building);
    }
}