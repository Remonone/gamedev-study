using Economy.Conditions;
using Types.Buildings;
using Types.Economy;
using Types.Economy.Modifiers;

namespace Types.Modifiers {
    public class RuleModifierDefinition : ModifierDefinition<RuleBasedContext> {
        
        public ConditionAsset[] Conditions;
        
        public override StatModifier? Resolve(RuleBasedContext context) {
            if(!VerifyConditions(context)) return null;
            
            return new StatModifier {
                Stat = Stat,
                Operation = Operation,
                Value = Value,
                Priority = Priority,
                ModifierId = Id
            };
        }

        private bool VerifyConditions(RuleBasedContext context) {
            if (Conditions == null) return true;
            foreach (var condition in Conditions) {
                if (!condition.Evaluate(context.Session, context.Building)) return false;
            }
            return true;
        }
    }
    
    public sealed class RuleBasedContext : IContext {
        public BuildingState Building;
        public SessionContext Session;
    }
}