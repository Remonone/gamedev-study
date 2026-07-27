namespace Data.Modifiers.Providers {
    public interface IModifierProvider {
        T Collect<T>(T target) where T : struct;
    }
}