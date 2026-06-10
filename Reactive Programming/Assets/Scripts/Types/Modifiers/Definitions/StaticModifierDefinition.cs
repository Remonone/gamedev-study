using System;
using Types.Buildings;
using Types.Economy.Modifiers;

namespace Types.Modifiers {
    
    [Serializable]
    public class StaticModifierDefinition : ModifierDefinition<StaticContext> {
        
        public override StatModifier? Resolve(StaticContext context) {
            if (!Target.Matches(context.Building)) return null;
            return new StatModifier {
                Stat = Stat,
                Operation = Operation,
                Value = Value,
                Priority = Priority,
                ModifierId = Id
            };
        }
    }
    
    public class StaticContext : IContext {
        public BuildingState Building;
    }
}