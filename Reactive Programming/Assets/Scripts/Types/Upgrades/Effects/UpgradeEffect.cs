using Types.Economy;
using UnityEngine;

namespace Types.Upgrades.Effects {
    public abstract class UpgradeEffect : ScriptableObject {
        public abstract void Apply(SessionContext context);
    }
}