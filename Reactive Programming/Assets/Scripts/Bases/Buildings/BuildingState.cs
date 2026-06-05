using Types.Economy;
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
        

        public float GetLevelBasedValue(StatType statType) {
            switch (statType) {
                case StatType.Income: return Definition.Income.GetValue(Level);
                case StatType.Cost: return Definition.Cost.GetValue(Level);
                case StatType.Frequency: return Definition.Frequency.GetValue(Level);
                case StatType.MultiplierCoefficient: return Definition.MultiplierCoefficient.GetValue(Level);
                case StatType.StabilityModifier: return Definition.StabilityModifier.GetValue(Level);
                case StatType.StabilityModifierMultiplier: return Definition.StabilityModifierMultiplier.GetValue(Level);
                case StatType.CriticalChance: return Definition.CriticalChance.GetValue(Level);
                case StatType.CriticalMultiplier: return Definition.CriticalMultiplier.GetValue(Level);
                default: return 0;
            }
        }
    }
}