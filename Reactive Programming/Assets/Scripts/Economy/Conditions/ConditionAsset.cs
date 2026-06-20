using Types.Modifiers.Definitions.Buildings;
using Types.Modifiers.Definitions;
using UnityEngine;

namespace Economy.Conditions {
    public abstract class ConditionAsset : ScriptableObject {
        public abstract bool Evaluate(ISessionContext context, BuildingState building);
    }
}