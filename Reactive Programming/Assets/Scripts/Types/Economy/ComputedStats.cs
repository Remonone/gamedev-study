using Types.Modifiers.Cost;
using Types.Values;

namespace Types.Modifiers {
    public struct ComputedStats {
        public Value ClickIncome;
        public Value Income;
        public float Frequency;
        public Price Cost;
        public float StabilityModifier;
        public float StabilityModifierMultiplier;
        public float MultiplierCoefficient;
        public float CriticalChance;
        public float CriticalMultiplier;
    }
}
