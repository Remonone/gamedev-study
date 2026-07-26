namespace Data.Modifiers {
    public interface IModifierContext {
        bool TryGet<TCapability>(out TCapability capability) where TCapability : class;
        
        TCapability Require<TCapability>() where TCapability : class;
    }
}