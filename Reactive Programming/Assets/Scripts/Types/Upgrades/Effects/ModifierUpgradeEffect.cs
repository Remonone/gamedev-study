using System.Collections.Generic;
using Types.Modifiers;
using UnityEngine;

namespace Types.Upgrades.Effects {
    
    [CreateAssetMenu(fileName = "Modifier Upgrade Effect", menuName = "Clicker/Upgrade Effect/Modifier Upgrade Effect", order = 0)]
    public class ModifierUpgradeEffect : UpgradeEffect {
        public List<ModifierDefinition> Rules;
    }
}