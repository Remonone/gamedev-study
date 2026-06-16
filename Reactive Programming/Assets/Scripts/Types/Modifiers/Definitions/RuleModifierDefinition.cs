using Economy.Conditions;
using Types.Enums;
using Types.Enums.Buildings;
using Types.Enums.Context;
using UnityEngine;

namespace Types.Enums {
    [CreateAssetMenu(fileName = "RuleModifierDefinition", menuName = "Clicker/Modifiers/Rule Modifier Definition", order = 0)]
    public class RuleModifierDefinition : ModifierDefinition{
        [SerializeReference] public ConditionAsset[] Conditions;

        private bool VerifyConditions(SessionCapability capability, BuildingState state) {
            if (Conditions == null) return true;
            foreach (var condition in Conditions) {
                if (!condition.Evaluate(capability.Session, state)) return false;
            }
            return true;
        }

        protected override bool CanResolve(IModifierContext context) {
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