using Types.Economy;
using Types.Economy.Cost;
using UnityEngine;

namespace Bases.Buildings {
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
        

        public decimal GetLevelBasedValue(StatType statType) {
            switch (statType) {
                case StatType.Income: return Definition.Income.Evaluate(Level);
                case StatType.Frequency: return Definition.Frequency.Evaluate(Level);
                case StatType.MultiplierCoefficient: return Definition.MultiplierCoefficient.Evaluate(Level);
                case StatType.StabilityModifier: return Definition.StabilityModifier.Evaluate(Level);
                case StatType.StabilityModifierMultiplier: return Definition.StabilityModifierMultiplier.Evaluate(Level);
                case StatType.CriticalChance: return Definition.CriticalChance.Evaluate(Level);
                case StatType.CriticalMultiplier: return Definition.CriticalMultiplier.Evaluate(Level);
                default: return 0;
            }
        }

        public Price GetPrice() {
            return Definition.Cost.Evaluate(Level);
        }
    }
}