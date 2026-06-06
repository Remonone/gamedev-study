using System;
using System.Collections.Generic;
using System.Linq;
using Bases.Buildings;
using Types.Economy;
using UnityEngine;

namespace Economy {
    public class StatResolver {
        public ComputedStats Resolve(BuildingState building, List<StatModifier> modifiers) {
            var result = new ComputedStats {
                Income = ResolveOne((float)building.GetLevelBasedValue(StatType.Income), StatType.Income, modifiers),
                Frequency = ResolveOne((float)building.GetLevelBasedValue(StatType.Frequency), StatType.Frequency, modifiers),
                Cost = ResolveOne((float)building.GetLevelBasedValue(StatType.Cost), StatType.Cost, modifiers),
                StabilityModifier = ResolveOne((float)building.GetLevelBasedValue(StatType.StabilityModifier), StatType.StabilityModifier, modifiers),
                StabilityModifierMultiplier = ResolveOne((float)building.GetLevelBasedValue(StatType.StabilityModifierMultiplier), StatType.StabilityModifierMultiplier, modifiers),
                MultiplierCoefficient = ResolveOne((float)building.GetLevelBasedValue(StatType.MultiplierCoefficient), StatType.MultiplierCoefficient, modifiers),
                CriticalChance = ResolveOne((float)building.GetLevelBasedValue(StatType.CriticalChance), StatType.CriticalChance, modifiers),
                CriticalMultiplier = ResolveOne((float)building.GetLevelBasedValue(StatType.CriticalMultiplier), StatType.CriticalMultiplier, modifiers)
            };

            result.Frequency = Mathf.Max(0.01f, result.Frequency);
            result.CriticalChance = Mathf.Clamp01(result.CriticalChance);
            return result;
        }

        private float ResolveOne(float baseValue, StatType stat, List<StatModifier> modifiers) {
            float flat = 0f;
            float percent = 0f;
            float mul = 1f;

            bool hasOverride = false;
            int overridePriority = int.MinValue;
            float overrideValue = baseValue;

            foreach (var modifier in modifiers.Where(modifier => modifier.Stat == stat)) {
                switch (modifier.Operation) {
                    case ModifierOp.AddFlat:
                        flat += modifier.Value;
                        break;
                    case ModifierOp.AddPercent:
                        percent += modifier.Value;
                        break;
                    case ModifierOp.Multiply:
                        mul *= modifier.Value;
                        break;
                    case ModifierOp.Override:
                        if (!hasOverride || modifier.Priority > overridePriority) {
                            hasOverride = true;
                            overridePriority = modifier.Priority;
                            overrideValue = modifier.Value;
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (hasOverride) return overrideValue;
            return ((baseValue + flat) * (1f + percent)) * mul;
        }
    }
}