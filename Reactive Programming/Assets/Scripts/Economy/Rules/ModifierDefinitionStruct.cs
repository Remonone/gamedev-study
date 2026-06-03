
using Types.Economy;

namespace Economy.Rules {
    [System.Serializable]
    public struct ModifierDefinition {
        public StatType Stat;
        public ModifierOp Operation;
        public float Value;
        public int Priority;
        public ModifierTarget Target;
    }
}
