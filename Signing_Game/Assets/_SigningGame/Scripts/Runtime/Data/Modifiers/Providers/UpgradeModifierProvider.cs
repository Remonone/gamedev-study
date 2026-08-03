using System.Linq;
using Services;
using Services.Locator;

namespace Data.Modifiers.Providers {
    public class UpgradeModifierProvider : IModifierProvider {
        
        private UpgradeService _upgradeService;
        
        public T Collect<T>(T target) where T : struct {
            var context = new ModifierContext();
            var result = target;
            var affectingModifiers = _upgradeService.OwnedUpgrades
                .SelectMany(upgrade => upgrade.Definition.Modifiers)
                .SelectMany(modifier => modifier.NumericModifiers)
                .Where(modifier => modifier != null)
                .Where(modifier => modifier.IsApplicable(typeof(T)));
            foreach (var modifier in affectingModifiers) {
                result = modifier.Apply(result, context);
            }
            return result;
        }

        public void Init(IServiceScope scope) {
            _upgradeService = scope.Get<UpgradeService>();
        }
    }
}