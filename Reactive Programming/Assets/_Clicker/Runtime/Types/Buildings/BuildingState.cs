using Types.Enums;
using Types.Modifiers;
using Types.Modifiers.Cost;
using Types.Values;
using UnityEngine;

namespace Types.Buildings {
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
            var level = new Value(Level);
            switch (statType) {
                case StatType.ClickIncome: return Definition.ClickIncome.Evaluate(level);
                case StatType.Income: return Definition.Income.Evaluate(level);
                case StatType.Frequency: return Definition.Frequency.Evaluate(level);
                case StatType.MultiplierCoefficient: return Definition.MultiplierCoefficient.Evaluate(level);
                case StatType.StabilityModifier: return Definition.StabilityModifier.Evaluate(level);
                case StatType.StabilityModifierMultiplier: return Definition.StabilityModifierMultiplier.Evaluate(level);
                case StatType.CriticalChance: return Definition.CriticalChance.Evaluate(level);
                case StatType.CriticalMultiplier: return Definition.CriticalMultiplier.Evaluate(level);
                default: return Value.Zero;
            }
        }

        public Price GetPrice() {
            return Definition.Cost.Evaluate(Level);
        }
    }
}
