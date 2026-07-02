using System.Collections.Generic;
using Types.QTE;
using UnityEngine;

namespace Types.Upgrades.Effects {
    [CreateAssetMenu(fileName = "QTE Upgrade Effect", menuName = "Clicker/Upgrade Effect/QTE Upgrade Effect", order = 0)]
    public class QteUpgradeEffect : UpgradeEffect {
        [SerializeReference, Tooltip("QTE improvements activated by this upgrade.")]
        public List<QteModifierEffect> Effects = new();
    }
}
