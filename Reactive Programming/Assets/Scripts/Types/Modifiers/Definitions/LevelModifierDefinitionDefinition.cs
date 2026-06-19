using Types.Enums.Buildings;
using Types.Enums.Context;
using Types.Enums.Cost.Formula;
using Types.Enums;
using UnityEngine;

namespace Types.Enums {
    [CreateAssetMenu(fileName = "LevelModifierDefinition", menuName = "Clicker/Modifiers/Level Modifier Definition", order = 0)]
    public class LevelModifierDefinitionDefinition : ModifierDefinition {
        [SerializeReference, Tooltip("Formula evaluated with the upgrade level; result multiplies Modifier.Value.")]
        public IFormula CalculationFormula;

        protected override bool CanResolve(IModifierContext context) {
            return context.TryGet<LevelCapability>(out _);
        }

        protected override StatModifier? ResolveInternal(BuildingState state, IModifierContext context) {
            context.TryGet(out LevelCapability levelCapability);
            return new StatModifier {
                ModifierId = Modifier.ModifierId,
                Operation = Modifier.Operation,
                Value = Modifier.Value * CalculationFormula.Evaluate(levelCapability.Level).ToSingle(),
                Stat = Modifier.Stat,
                Priority = Modifier.Priority
            };
        }
    }

}
