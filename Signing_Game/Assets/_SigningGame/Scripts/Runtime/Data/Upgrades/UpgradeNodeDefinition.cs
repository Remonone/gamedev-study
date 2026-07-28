using Data.Formulas;
using Data.Modifiers;
using UnityEngine;
using Utils;

namespace Data.Upgrades {
    [CreateAssetMenu(menuName = "Upgrades/Upgrade Node", fileName = "Upgrade Node")]
    public class UpgradeNodeDefinition : ScriptableObject {
        public string Id;
        public string Name;
        public string Description;
        public int MaxLevel;
        public Sprite Icon;
        [SerializeReference] public IFormula CostFormula;
        [SerializeReference] public ModifierDefinition[] Modifiers;
        
    }
}