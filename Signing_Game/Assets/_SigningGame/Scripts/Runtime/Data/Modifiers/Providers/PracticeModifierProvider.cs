using System;
using Data.Research;
using Services;
using Services.Locator;

namespace Data.Modifiers.Providers {
    public sealed class PracticeModifierProvider : IModifierProvider {
        private ResearchService _research;

        public T Collect<T>(T target) where T : struct {
            if (_research == null || !_research.IsUnlocked) return target;
            T result = target;
            var active = _research.ActivePractices;
            for (int index = 0; index < active.Count; index++) {
                ActivePracticeState state = active[index];
                ModifierDefinition[] definitions = state.Definition.Modifiers;
                if (definitions == null) continue;
                var context = new ModifierContext()
                    .Add(new LevelModifierCapability(1))
                    .Add(new ModifierEffectivenessCapability(state.Effectiveness));
                for (int definitionIndex = 0; definitionIndex < definitions.Length; definitionIndex++) {
                    ModifierDefinition definition = definitions[definitionIndex];
                    if (definition?.NumericModifiers == null) continue;
                    for (int modifierIndex = 0; modifierIndex < definition.NumericModifiers.Count; modifierIndex++) {
                        NumericModifierDefinition modifier = definition.NumericModifiers[modifierIndex];
                        if (modifier != null && modifier.IsApplicable(result)) {
                            result = modifier.Apply(result, context, true);
                        }
                    }
                }
            }
            return result;
        }

        public void Init(IServiceScope scope) {
            _research = scope.Get<ResearchService>();
        }
    }
}
