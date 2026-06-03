using System.Collections.Generic;
using Bases.Buildings;
using Economy.Rules;
using Types.Economy;

namespace Economy.Providers {
    public sealed class RuleBasedModifierProvider : IModifierProvider {

        private readonly ModifierRule[] _rules;
        
        public RuleBasedModifierProvider(ModifierRule[] rules) {
            _rules = rules;
        }
        
        public void Collect(SessionContext context, BuildingState building, List<StatModifier> modifiers) {
            if (_rules == null) return;
            
            foreach (var rule in _rules) {
                if (rule == null || !rule.IsActive(context, building)) continue;
                
                rule.AppendModifiers(building, modifiers);
            }
        }
    }
}