using Types.Buildings;
using Types.Economy.Cost.Formula;
using Types.Economy.Modifiers;

namespace Types.Modifiers {
    public class LevelModifierDefinition : ModifierDefinition<LevelContext> {
        public IFormula CalculationFormula;
        
        public override StatModifier? Resolve(LevelContext context) {
            if(context.Level < 1) return null;
            return new StatModifier {
                Stat = Stat,
                Operation = Operation,
                Value = Value * (float)CalculationFormula.Evaluate(context.Level),
                Priority = Priority,
                ModifierId = Id
            };
        }
    }

    public class LevelContext : IContext {
        public int Level;
        public BuildingState Building;
    }
}