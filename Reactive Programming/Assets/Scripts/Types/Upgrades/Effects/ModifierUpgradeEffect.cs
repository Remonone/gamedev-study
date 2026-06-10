using System.Collections.Generic;
using Economy.Rules;
using Types.Economy;

namespace Types.Upgrades.Effects {
    
    public class ModifierUpgradeEffect : UpgradeEffect {

        public List<ModifierRule> _rules;
        
        public override void Apply(SessionContext context) { 
        }
    }
}