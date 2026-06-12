using Types.Buildings;
using Types.Economy.Cost.Formula;
using Types.Economy.Modifiers;
using Types.Modifiers.Context;
using UnityEngine;

namespace Types.Modifiers {
    [CreateAssetMenu(fileName = "LevelModifierDefinition", menuName = "Clicker/Modifiers/Level Modifier Definition", order = 0)]
    public class LevelModifierDefinitionDefinition : ModifierDefinition {
        [SerializeReference] public IFormula CalculationFormula;

        protected override bool CanResolve(IModifierContext context) {
            return context.TryGet<LevelCapability>(out _);
        }

        protected override StatModifier? ResolveInternal(BuildingState state, IModifierContext context) {
            context.TryGet(out LevelCapability levelCapability);
            return new StatModifier {
                ModifierId = Modifier.ModifierId,
                Operation = Modifier.Operation,
                Value = Modifier.Value * (float)CalculationFormula.Evaluate(levelCapability.Level),
                Stat = Modifier.Stat,
                Priority = Modifier.Priority
            };
        }
    }

}