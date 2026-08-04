using Services;
using Services.Locator;

namespace Data.Modifiers.Providers {
    public class UpgradeModifierProvider : IModifierProvider {
        
        private UpgradeService _upgradeService;
        
        public T Collect<T>(T target) where T : struct {
            T result = target;
            foreach (var upgrade in _upgradeService.OwnedUpgrades) {
                var context = new ModifierContext()
                    .Add(new LevelModifierCapability(upgrade.Level))
                    .Add(new ModifierEffectivenessCapability(upgrade.Effectiveness));
                ModifierDefinition[] definitions = upgrade.Definition.Modifiers;
                if (definitions == null) continue;

                foreach (ModifierDefinition definition in definitions) {
                    if (definition?.NumericModifiers == null) continue;
                    foreach (NumericModifierDefinition modifier in definition.NumericModifiers) {
                        if (modifier == null || !modifier.IsApplicable(result)) continue;
                        result = modifier.Apply(result, context);
                    }
                }
            }
            return result;
        }

        public void Init(IServiceScope scope) {
            _upgradeService = scope.Get<UpgradeService>();
        }
    }
}
