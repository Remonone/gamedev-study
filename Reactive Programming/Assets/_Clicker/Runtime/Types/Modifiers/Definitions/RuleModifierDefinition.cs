using Economy.Conditions;
using Types.Buildings;
using Types.Modifiers.Definitions.Context;
using UnityEngine;

namespace Types.Modifiers.Definitions {
    [CreateAssetMenu(fileName = "RuleModifierDefinition", menuName = "Clicker/Modifiers/Rule Modifier Definition", order = 0)]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp")]
    public class RuleModifierDefinition : ModifierDefinition{
        [SerializeReference, Tooltip("All conditions that must pass before this modifier is applied. Empty list means always applies.")]
        public ConditionAsset[] Conditions;

        private bool VerifyConditions(SessionCapability capability, BuildingState state) {
            if (Conditions == null) return true;
            foreach (var condition in Conditions) {
                if (!condition.Evaluate(capability.Session, state)) return false;
            }
            return true;
        }

        public override bool CanResolve(IModifierContext context) {
            return context.TryGet<SessionCapability>(out _);
        }

        protected override StatModifier? ResolveInternal(BuildingState state, IModifierContext context) {
            context.TryGet(out SessionCapability sessionCapability);
            if (!VerifyConditions(sessionCapability, state)) return null;
            
            return new StatModifier {
                Stat = Modifier.Stat,
                Operation = Modifier.Operation,
                Value = Modifier.Value,
                Priority = Modifier.Priority,
                ModifierId = Modifier.ModifierId
            };
        }
    }
}
