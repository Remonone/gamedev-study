using UnityEngine;

namespace Types.Enums.Upgrades.Effects {
    [CreateAssetMenu(fileName = "Unlock Upgrade", menuName = "Clicker/Upgrade Effect/Unlock Upgrade", order = 0)]
    public class UnlockUpgrade : UpgradeEffect {
        public string UnlockId;
    }
}