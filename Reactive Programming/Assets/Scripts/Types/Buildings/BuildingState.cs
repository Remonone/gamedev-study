using Types.Modifiers.Definitions.Cost;
using Types.Modifiers.Definitions.Values;
using UnityEngine;

namespace Types.Modifiers.Definitions.Buildings {
    public sealed class BuildingState {
        public BuildingDefinition Definition;
        public int Level;
        public ComputedStats Cache;
        public bool IsDirty;
        public double LastTimeActivated;
        
        public BuildingState(BuildingDefinition definition, int level) {
            Definition = definition;
            Level = level;
            IsDirty = true;
            LastTimeActivated = Time.timeAsDouble;
        }
        

        public Value GetLevelBasedValue(StatType statType) {
            switch (statType) {
                case StatType.ClickIncome: return Definition.ClickIncome.Evaluate(Level);
                case StatType.Income: return Definition.Income.Evaluate(Level);
                case StatType.Frequency: return Definition.Frequency.Evaluate(Level);
                case StatType.MultiplierCoefficient: return Definition.MultiplierCoefficient.Evaluate(Level);
                case StatType.StabilityModifier: return Definition.StabilityModifier.Evaluate(Level);
                case StatType.StabilityModifierMultiplier: return Definition.StabilityModifierMultiplier.Evaluate(Level);
                case StatType.CriticalChance: return Definition.CriticalChance.Evaluate(Level);
                case StatType.CriticalMultiplier: return Definition.CriticalMultiplier.Evaluate(Level);
                default: return Value.Zero;
            }
        }

        public Price GetPrice() {
            return Definition.Cost.Evaluate(Level);
        }
    }
}
