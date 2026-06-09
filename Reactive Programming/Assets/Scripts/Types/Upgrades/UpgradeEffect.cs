using UnityEngine;

namespace Types.Upgrades {
    public abstract class UpgradeEffect : ScriptableObject {
        public abstract void Apply();
        public abstract void Remove();
    }
}