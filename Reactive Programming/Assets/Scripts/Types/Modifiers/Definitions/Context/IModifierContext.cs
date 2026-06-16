namespace Types.Enums.Context {
    public interface IModifierContext {
        bool TryGet<TCapability>(out TCapability capability) where TCapability : class;
    }
}