using Types.Buildings;
using Types.Modifiers.Definitions.Context;
using Types.Modifiers.Cost.Formula;
using Types.Values;
using UnityEngine;

namespace Types.Modifiers.Definitions {
    [CreateAssetMenu(fileName = "LevelModifierDefinition", menuName = "Clicker/Modifiers/Level Modifier Definition", order = 0)]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp")]
    public class LevelModifierDefinitionDefinition : ModifierDefinition {
        [SerializeReference, Tooltip("Formula evaluated with the upgrade level; result multiplies Modifier.Value.")]
        public IFormula CalculationFormula;

        public override bool CanResolve(IModifierContext context) {
            return context.TryGet<LevelCapability>(out _);
        }

        protected override StatModifier? ResolveInternal(BuildingState state, IModifierContext context) {
            context.TryGet(out LevelCapability levelCapability);
            return new StatModifier {
                ModifierId = Modifier.ModifierId,
                Operation = Modifier.Operation,
                Value = Modifier.Value * CalculationFormula.Evaluate(new Value(levelCapability.Level)).ToSingle(),
                Stat = Modifier.Stat,
                Priority = Modifier.Priority
            };
        }
    }

}
