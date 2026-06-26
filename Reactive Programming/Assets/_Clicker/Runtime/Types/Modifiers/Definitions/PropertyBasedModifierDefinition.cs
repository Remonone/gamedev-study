using Types.Buildings;
using Types.Enums;
using Types.Modifiers.Cost.Formula;
using Types.Modifiers.Definitions.Context;

namespace Types.Modifiers.Definitions {
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp")]
    public class PropertyBasedModifierDefinition : ModifierDefinition {

        public IFormula CalculationFormula;
        public StatType Property;
        
        public override bool CanResolve(IModifierContext context) {
            return true;
        }

        protected override StatModifier? ResolveInternal(BuildingState state, IModifierContext context) {
            return new StatModifier {
                ModifierId = Modifier.ModifierId,
                Operation = Modifier.Operation,
                Stat = Modifier.Stat,
                Value = CalculationFormula.Evaluate(state.GetLevelBasedValue(Property).ToDouble()).ToSingle(),
                Priority = Modifier.Priority
            };
        }
    }
}
