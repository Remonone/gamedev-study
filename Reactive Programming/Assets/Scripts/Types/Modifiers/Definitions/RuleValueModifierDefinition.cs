using Economy.Conditions;
using Types.Buildings;
using Types.Economy;
using Types.Economy.Cost.Formula;
using Types.Economy.Modifiers;

namespace Types.Modifiers {
    public class RuleValueModifierDefinition : ModifierDefinition<RuleValueContext> {
        
        public ConditionAsset[] Conditions;
        public IFormula CalculationFormula;
        
        public override StatModifier? Resolve(RuleValueContext context) {
            if (context.Level < 1) return null;
            if(Conditions == null) return null;
            if (!VerifyConditions(context)) return null;

            return new StatModifier {
                Stat = Stat,
                Operation = Operation,
                Value = Value * (float)CalculationFormula.Evaluate(context.Level),
                Priority = Priority,
                ModifierId = Id
            };
        }
        
        private bool VerifyConditions(RuleValueContext context) {
            if (Conditions == null) return true;
            foreach (var condition in Conditions) {
                if (!condition.Evaluate(context.Session, context.Building)) return false;
            }
            return true;
        }
    }

    public sealed class RuleValueContext : IContext {
        public BuildingState Building;
        public int Level;
        public SessionContext Session;
    }
}