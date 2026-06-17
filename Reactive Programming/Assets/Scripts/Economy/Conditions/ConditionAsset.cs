using Types.Enums.Buildings;
using Types.Enums;
using UnityEngine;

namespace Economy.Conditions {
    public abstract class ConditionAsset : ScriptableObject {
        public abstract bool Evaluate(ISessionContext context, BuildingState building);
    }
}