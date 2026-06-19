using System.Collections.Generic;
using Types.Enums;
using UnityEngine;

namespace Types.Enums.Upgrades.Effects {
    
    [CreateAssetMenu(fileName = "Modifier Upgrade Effect", menuName = "Clicker/Upgrade Effect/Modifier Upgrade Effect", order = 0)]
    public class ModifierUpgradeEffect : UpgradeEffect {
        [Tooltip("Modifier definitions activated by this upgrade effect.")]
        public List<ModifierDefinition> Rules;
    }
}
