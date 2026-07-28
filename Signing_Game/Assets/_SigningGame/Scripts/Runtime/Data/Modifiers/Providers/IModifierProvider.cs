using Services.Locator;

namespace Data.Modifiers.Providers {
    public interface IModifierProvider {
        T Collect<T>(T target) where T : struct;
        void Init(IServiceScope scope);
    }
}