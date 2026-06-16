using System;
using Types.Enums.Target;
using Types.Enums.Buildings;
using Types.Enums.Context;
using Types.Enums;
using UnityEngine;

namespace Types.Enums {
    
    [CreateAssetMenu(fileName = "StaticModifierDefinition", menuName = "Clicker/Modifiers/Static Modifier Definition", order = 0)]
    public class StaticModifierDefinitionDefinition : ModifierDefinition {
        
        protected override bool CanResolve(IModifierContext context) {
            return true;
        }

        protected override StatModifier? ResolveInternal(BuildingState state, IModifierContext context) {
            return new StatModifier {
                Stat = Modifier.Stat,
                Operation = Modifier.Operation,
                Value = Modifier.Value,
                Priority = Modifier.Priority,
                ModifierId = Modifier.ModifierId
            };
        }
    }
}