using System;
using System.Collections.Generic;
using Services;
using Types;
using Types.Buildings;
using Types.Enums;
using Types.Modifiers;
using Types.Modifiers.Definitions.Context;

namespace Economy.Providers {
    public class PracticesModifierProvider : IModifierProvider {
        private readonly IReadOnlyList<Practice> _ownedPractices;

        public PracticesModifierProvider(PracticeService practiceService) {
            _ownedPractices = practiceService.OwnedPracticeDefinitions;
        }
        
        public void Collect(ISessionContext context, BuildingState building, List<StatModifier> modifiers) {
            var modifierContext = new ModifierContext();
            foreach (var practice in _ownedPractices) {
                foreach (var definition in practice.StatModifiers) {
                    if (definition == null || definition.Target == null || !definition.CanResolve(modifierContext)) {
                        continue;
                    }

                    var resolved = definition.Resolve(building, modifierContext);
                    if (resolved.HasValue) {
                        modifiers.Add(resolved.Value);
                    }
                }

                foreach (var effect in practice.InfluenceEffects) {
                    if (effect?.Target == null || !effect.Target.Matches(building)) {
                        continue;
                    }

                    modifiers.Add(new StatModifier {
                        Stat = effect.Stat,
                        Operation = effect.Operation,
                        Value = effect.BaseValue + GetInfluenceValue(context, effect) * effect.ValuePerInfluence,
                        Priority = effect.Priority,
                        ModifierId = string.IsNullOrWhiteSpace(effect.ModifierId) ? $"practice_{practice.Id}_{effect.Stat}" : effect.ModifierId
                    });
                }
            }
        }
        
        private int GetInfluenceValue(ISessionContext context, InfluencePracticeEffect effect) {
            if (context == null) {
                return 0;
            }

            if (!effect.UseAllInfluence) {
                return context.GetInfluenceValue(effect.SourceInfluence);
            }

            var total = 0;
            foreach (GovernmentInteractionType type in Enum.GetValues(typeof(GovernmentInteractionType))) {
                total += context.GetInfluenceValue(type);
            }
            return total;
        }
    }
}
