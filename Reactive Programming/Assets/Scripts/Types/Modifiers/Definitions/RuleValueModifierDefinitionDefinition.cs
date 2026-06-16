using Economy.Conditions;
using Types.Enums.Buildings;
using Types.Enums.Context;
using Types.Enums.Cost.Formula;
using Types.Enums;
using UnityEngine;

namespace Types.Enums {
    [CreateAssetMenu(fileName = "RuleValueModifierDefinition", menuName = "Clicker/Modifiers/Rule Value Modifier Definition", order = 0)]
    public class RuleValueModifierDefinitionDefinition : ModifierDefinition {
        [SerializeReference] public ConditionAsset[] Conditions;
        [SerializeReference] public IFormula CalculationFormula;
        
        private bool VerifyConditions(SessionCapability capability, BuildingState state) {
            if (Conditions == null) return true;
            foreach (var condition in Conditions) {
                if (!condition.Evaluate(capability.Session, state)) return false;
            }
            return true;
        }

        protected override bool CanResolve(IModifierContext context) {
            return context.TryGet<SessionCapability>(out _) && context.TryGet<LevelCapability>(out _);
        }

        protected override StatModifier? ResolveInternal(BuildingState state, IModifierContext context) {
            context.TryGet(out SessionCapability sessionCapability);
            if (!VerifyConditions(sessionCapability, state)) return null;
            
            context.TryGet(out LevelCapability levelCapability);
            
            return new StatModifier {
                Stat = Modifier.Stat,
                Operation = Modifier.Operation,
                Value = Modifier.Value * (float)CalculationFormula.Evaluate(levelCapability.Level),
                Priority = Modifier.Priority,
                ModifierId = Modifier.ModifierId
            };
        }
    }
}