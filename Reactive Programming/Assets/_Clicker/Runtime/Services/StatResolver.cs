using System;
using System.Collections.Generic;
using System.Linq;
using Types.Buildings;
using Types.Enums;
using Types.Modifiers;
using Types.Modifiers.Cost;
using Types.Values;
using UnityEngine;

namespace Economy {
    public class StatResolver {
        public ComputedStats Resolve(BuildingState building, List<StatModifier> modifiers) {
            var result = new ComputedStats {
                ClickIncome = ResolveValue(building.GetLevelBasedValue(StatType.ClickIncome), StatType.ClickIncome, modifiers),
                Income = ResolveValue(building.GetLevelBasedValue(StatType.Income), StatType.Income, modifiers),
                Frequency = ResolveFloat(building.GetLevelBasedValue(StatType.Frequency).ToSingle(), StatType.Frequency, modifiers),
                Cost = ResolvePrice(building.GetPrice(), modifiers),
                StabilityModifier = ResolveFloat(building.GetLevelBasedValue(StatType.StabilityModifier).ToSingle(), StatType.StabilityModifier, modifiers),
                StabilityModifierMultiplier = ResolveFloat(building.GetLevelBasedValue(StatType.StabilityModifierMultiplier).ToSingle(), StatType.StabilityModifierMultiplier, modifiers),
                MultiplierCoefficient = ResolveFloat(building.GetLevelBasedValue(StatType.MultiplierCoefficient).ToSingle(), StatType.MultiplierCoefficient, modifiers),
                CriticalChance = ResolveFloat(building.GetLevelBasedValue(StatType.CriticalChance).ToSingle(), StatType.CriticalChance, modifiers),
                CriticalMultiplier = ResolveFloat(building.GetLevelBasedValue(StatType.CriticalMultiplier).ToSingle(), StatType.CriticalMultiplier, modifiers)
            };

            result.Frequency = Mathf.Max(0.01f, result.Frequency);
            result.CriticalChance = Mathf.Clamp01(result.CriticalChance);
            return result;
        }

        private Price ResolvePrice(Price price, List<StatModifier> modifiers) {
            var flat = Value.Zero;
            var percent = 0d;
            var mul = 1d;
            
            foreach (var modifier in modifiers.Where(modifier => modifier.Stat == StatType.Cost)) {
                switch (modifier.Operation) {
                    case ModifierOp.AddFlat:
                        flat += new Value(modifier.Value);
                        break;
                    case ModifierOp.AddPercent:
                        percent += modifier.Value;
                        break;
                    case ModifierOp.Multiply:
                        mul *= modifier.Value;
                        break;
                }
            }

            for (int i = 0; i < price.Entries.Length; i++) {
                var withoutFlat = price.Entries[i].Price - flat;
                var divisor = (1d + percent) * mul;
                price.Entries[i].Price = ScaleNonNegative(withoutFlat ?? Value.Zero, divisor <= 0d ? 0d : 1d / divisor);
            }

            return price;
        }

        private Value ResolveValue(Value baseValue, StatType stat, List<StatModifier> modifiers) {
            var flat = Value.Zero;
            var percent = 0d;
            var mul = 1d;

            bool hasOverride = false;
            int overridePriority = int.MinValue;
            var overrideValue = baseValue;

            foreach (var modifier in modifiers.Where(modifier => modifier.Stat == stat)) {
                switch (modifier.Operation) {
                    case ModifierOp.AddFlat:
                        flat += new Value(modifier.Value);
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
                            overrideValue = new Value(modifier.Value);
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (hasOverride) return overrideValue;
            return ScaleNonNegative(baseValue + flat, (1d + percent) * mul);
        }

        private static Value ScaleNonNegative(Value value, double multiplier) {
            return multiplier <= 0d ? Value.Zero : value * multiplier;
        }

        private float ResolveFloat(float baseValue, StatType stat, List<StatModifier> modifiers) {
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
