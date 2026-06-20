using Types.Modifiers.Definitions.Buildings;
using Types.Modifiers.Definitions.Context;
using UnityEngine;

namespace Types.Modifiers.Definitions {
    
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