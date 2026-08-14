using Data.Upgrades;
using Services;
using Services.Locator;

namespace Data.Modifiers.Providers {
    public sealed class MetaUpgradeModifierProvider : IModifierProvider {
        private MetaProgressionService _meta;

        public T Collect<T>(T target) where T : struct {
            if (_meta == null) return target;
            T result = target;
            foreach (UpgradeNodeState upgrade in _meta.OwnedMetaUpgrades) {
                var context = new ModifierContext()
                    .Add(new LevelModifierCapability(upgrade.Level))
                    .Add(new ModifierEffectivenessCapability(1f));
                ModifierDefinition[] definitions = upgrade.Definition.Modifiers;
                if (definitions == null) continue;
                for (int definitionIndex = 0; definitionIndex < definitions.Length; definitionIndex++) {
                    ModifierDefinition definition = definitions[definitionIndex];
                    if (definition?.NumericModifiers == null) continue;
                    for (int modifierIndex = 0; modifierIndex < definition.NumericModifiers.Count; modifierIndex++) {
                        NumericModifierDefinition modifier = definition.NumericModifiers[modifierIndex];
                        if (modifier != null && modifier.IsApplicable(result)) result = modifier.Apply(result, context);
                    }
                }
            }
            return result;
        }

        public void Init(IServiceScope scope) {
            _meta = scope.Get<MetaProgressionService>();
        }
    }
}
