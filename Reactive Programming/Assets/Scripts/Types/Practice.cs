using System.Collections.Generic;
using Types.Buildings;
using Economy.Rules;
using Types.Economy;
using Types.Economy.Modifiers;
using UnityEngine;

namespace Types {
    [CreateAssetMenu(fileName = "Artifact", menuName = "Clicker/Artifact", order = 0)]
    public class Practice : ScriptableObject {
        public List<ModifierRule> _rules;
        
        public void ApplyRules(BuildingState building, List<StatModifier> modifiers) {
            foreach (var rule in _rules) {
                rule.AppendModifiers(building, modifiers);       
            }
        }
    }
}