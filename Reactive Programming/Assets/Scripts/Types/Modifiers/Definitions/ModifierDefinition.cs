using Types.Economy;
using Types.Economy.Modifiers;
using Types.Economy.Modifiers.Target;

namespace Types.Modifiers {
    
    public interface IContext {}
    
    public abstract class ModifierDefinition<T> where T : IContext {
        public string Id;
        public StatType Stat;
        public ModifierOp Operation;
        public float Value;
        public int Priority;
        public ModifierTarget Target;
        
        public abstract StatModifier? Resolve(T context);
    }
}