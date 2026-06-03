using Bases.Buildings;
using Types.Economy;
using UnityEngine;

namespace Economy.Conditions {
    public abstract class ConditionAsset : ScriptableObject {
        public abstract bool Evaluate(SessionContext context, BuildingState building);
    }
}