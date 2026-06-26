using UnityEngine;

namespace Types.Upgrades.Effects {
    [CreateAssetMenu(fileName = "Unlock Upgrade", menuName = "Clicker/Upgrade Effect/Unlock Upgrade", order = 0)]
    public class UnlockUpgrade : UpgradeEffect {
        [Tooltip("Id passed to UnlockService when this upgrade is purchased.")]
        public string UnlockId;
    }
}
