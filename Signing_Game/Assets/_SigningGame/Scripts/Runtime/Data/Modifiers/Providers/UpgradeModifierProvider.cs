namespace Data.Modifiers.Providers {
    public class UpgradeModifierProvider : IModifierProvider {
        
        
        
        public T Collect<T>(T target) where T : struct {
            var context = new ModifierContext();
            // TODO: Implement Upgrade Service
            return target;
        }
    }
}