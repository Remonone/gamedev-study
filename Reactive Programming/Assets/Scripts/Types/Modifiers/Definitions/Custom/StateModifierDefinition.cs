using System;
using Types.Modifiers.Definitions.Buildings;
using Types.Modifiers.Definitions.Context;

namespace Types.Modifiers.Definitions.Custom {
    public class StateModifierDefinition : IModifier {
        public bool CanResolve(IModifierContext context) {
            return context.TryGet<SessionCapability>(out _) && context.TryGet<TypeCapability>(out _);
        }

        public StatModifier? Resolve(BuildingState state, IModifierContext context) {
            if (!CanResolve(context))
                throw new InvalidOperationException($"Cannot resolve context: {context.ToString()} for {GetType().Name}");
            context.TryGet<TypeCapability>(out var typeCapability);
            context.TryGet<SessionCapability>(out var sessionCapability);
            var type = typeCapability.Type;
            var session = sessionCapability.Session;

            return ResolveOnType(type, session);
        }

        private StatModifier? ResolveOnType(GovernmentInteractionType type, ISessionContext session) {
            var influenceValue = session.GetInfluenceValue(type);
            if (influenceValue <= 1) {
                return null;
            }
            var modifier = new StatModifier {
                ModifierId = $"State_{type}",
                Priority = 1
            };
            Func<int, float> operation;
            switch (type) {
                case GovernmentInteractionType.FireFighterStation:
                    modifier.Stat = StatType.Frequency;
                    modifier.Operation = ModifierOp.AddPercent;
                    operation = value => (float)(Math.Log10(value) / 2f);
                    break;
                case GovernmentInteractionType.Hospital:
                    modifier.Stat = StatType.MultiplierCoefficient;
                    modifier.Operation = ModifierOp.Multiply;
                    operation = value => (float)(Math.Log10(value) / 2f);
                    break;
                case GovernmentInteractionType.Court:
                    modifier.Stat = StatType.CriticalMultiplier;
                    modifier.Operation = ModifierOp.Multiply;
                    operation = value => (float)Math.Log10(value);
                    break;
                default:
                    return null;
            }

            modifier.Value = operation(influenceValue);
            return modifier;
        }
    }
}