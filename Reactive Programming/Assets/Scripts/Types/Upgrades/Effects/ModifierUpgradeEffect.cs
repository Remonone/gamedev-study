using System.Collections.Generic;
using Types.Enums;
using UnityEngine;

namespace Types.Enums.Upgrades.Effects {
    
    [CreateAssetMenu(fileName = "Modifier Upgrade Effect", menuName = "Clicker/Upgrade Effect/Modifier Upgrade Effect", order = 0)]
    public class ModifierUpgradeEffect : UpgradeEffect {
        public List<ModifierDefinition> Rules;
    }
}