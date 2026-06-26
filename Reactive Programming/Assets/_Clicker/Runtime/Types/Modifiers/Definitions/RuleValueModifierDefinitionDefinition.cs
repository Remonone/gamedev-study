using Economy.Conditions;
using Types.Buildings;
using Types.Modifiers.Definitions.Context;
using Types.Modifiers.Cost.Formula;
using UnityEngine;

namespace Types.Modifiers.Definitions {
    [CreateAssetMenu(fileName = "RuleValueModifierDefinition", menuName = "Clicker/Modifiers/Rule Value Modifier Definition", order = 0)]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp")]
    public class RuleValueModifierDefinitionDefinition : ModifierDefinition {
        [SerializeReference, Tooltip("All conditions that must pass before this modifier is applied. Empty list means always applies.")]
        public ConditionAsset[] Conditions;
        [SerializeReference, Tooltip("Formula evaluated with the upgrade level; result multiplies Modifier.Value.")]
        public IFormula CalculationFormula;
        
        private bool VerifyConditions(SessionCapability capability, BuildingState state) {
            if (Conditions == null) return true;
            foreach (var condition in Conditions) {
                if (!condition.Evaluate(capability.Session, state)) return false;
            }
            return true;
        }

        public override bool CanResolve(IModifierContext context) {
            return context.TryGet<SessionCapability>(out _) && context.TryGet<LevelCapability>(out _);
        }

        protected override StatModifier? ResolveInternal(BuildingState state, IModifierContext context) {
            context.TryGet(out SessionCapability sessionCapability);
            if (!VerifyConditions(sessionCapability, state)) return null;
            
            context.TryGet(out LevelCapability levelCapability);
            
            return new StatModifier {
                Stat = Modifier.Stat,
                Operation = Modifier.Operation,
                Value = Modifier.Value * CalculationFormula.Evaluate(levelCapability.Level).ToSingle(),
                Priority = Modifier.Priority,
                ModifierId = Modifier.ModifierId
            };
        }
    }
}
