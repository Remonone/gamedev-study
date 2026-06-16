using Types.Enums.Buildings;
using UnityEngine;

namespace Types.Enums.Target {
    public abstract class ModifierTarget : ScriptableObject {
        public abstract bool Matches(BuildingState building);
    }
}