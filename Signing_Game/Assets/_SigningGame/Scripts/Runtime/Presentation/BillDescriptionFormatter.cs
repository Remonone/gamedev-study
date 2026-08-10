using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Data.Bills;
using Data.Modifiers;
using Data.Modifiers.Calculation;
using Services;
using UnityEngine;
using Utils;

namespace Presentation {
    public static class BillDescriptionFormatter {
        public static string FormatCatalog(BillService bills, GeneratedBillOption option) {
            double strength = bills.ResolveCatalogBaseRewardStrength(option);
            return Format(bills, option.Reward, option, strength, null);
        }

        public static string FormatActive(BillService bills, ActiveBillState active) {
            return Format(bills, active.Option.Reward, active.Option, active.SavedBaseRewardStrength, null);
        }

        public static string FormatCompleted(BillService bills, BillCompletionRecord completion) {
            return Format(
                bills,
                completion.Reward,
                completion.Option,
                completion.SavedBaseRewardStrength,
                completion);
        }

        public static IReadOnlyList<BillStatisticPresentationModel> BuildModifierRows(
            BillService bills,
            BillCompletionRecord completion) {
            var rows = new List<BillStatisticPresentationModel>();
            double effectiveness = bills.ResolveCompletionEffectiveness(completion.SavedBaseRewardStrength);
            foreach (NumericModifierDefinition modifier in EnumerateModifiers(completion.Reward)) {
                string label = ResolveModifierLabel(completion.Reward, modifier);
                string description = ResolveModifierDescription(completion.Reward, modifier);
                rows.Add(new BillStatisticPresentationModel(
                    label,
                    FormatModifierValue(modifier, effectiveness),
                    description));
            }
            return rows;
        }

        private static string Format(
            BillService bills,
            BillRewardDefinition reward,
            GeneratedBillOption option,
            double baseStrength,
            BillCompletionRecord completion) {
            string description = reward.Description ?? string.Empty;
            if (description.Length == 0) return description;
            var modifiers = new Dictionary<string, NumericModifierDefinition>(StringComparer.Ordinal);
            foreach (NumericModifierDefinition modifier in EnumerateModifiers(reward)) {
                if (!string.IsNullOrWhiteSpace(modifier.Id)) modifiers.TryAdd(modifier.Id, modifier);
            }

            double effectiveness = bills.ResolveCompletionEffectiveness(baseStrength);
            var result = new StringBuilder(description.Length + 32);
            int position = 0;
            while (position < description.Length) {
                int start = description.IndexOf("${", position, StringComparison.Ordinal);
                if (start < 0) {
                    result.Append(description, position, description.Length - position);
                    break;
                }
                result.Append(description, position, start - position);
                int end = description.IndexOf('}', start + 2);
                if (end < 0) {
                    result.Append(description, start, description.Length - start);
                    break;
                }
                string token = description.Substring(start + 2, end - start - 2);
                if (string.Equals(token, "activeGeneration", StringComparison.Ordinal)) {
                    double bonus = option == null || completion != null
                        ? 0d
                        : bills.ResolveActiveGenerationBonus(option, baseStrength);
                    result.Append('+').Append((bonus * 100d).ToString("0.##", CultureInfo.InvariantCulture)).Append('%');
                }
                else if (string.Equals(token, "moneyReward", StringComparison.Ordinal)) {
                    if (completion != null) {
                        result.Append(completion.HasCompletionPayout
                            ? $"{completion.ActualCompletionPayout}$"
                            : "Unavailable (legacy save)");
                    }
                    else if (option != null) result.Append(bills.ResolveExpectedCompletionPayout(option, baseStrength)).Append('$');
                    else result.Append("Unavailable");
                }
                else if (modifiers.TryGetValue(token, out NumericModifierDefinition modifier)) {
                    result.Append(FormatModifierValue(modifier, effectiveness));
                }
                else result.Append(description, start, end - start + 1);
                position = end + 1;
            }
            return result.ToString();
        }

        private static IEnumerable<NumericModifierDefinition> EnumerateModifiers(BillRewardDefinition reward) {
            ModifierDefinition[] definitions = reward.CompletionModifiers;
            if (definitions == null) yield break;
            for (int definitionIndex = 0; definitionIndex < definitions.Length; definitionIndex++) {
                ModifierDefinition definition = definitions[definitionIndex];
                if (definition?.NumericModifiers == null) continue;
                for (int modifierIndex = 0; modifierIndex < definition.NumericModifiers.Count; modifierIndex++) {
                    NumericModifierDefinition modifier = definition.NumericModifiers[modifierIndex];
                    if (modifier != null) yield return modifier;
                }
            }
        }

        private static string FormatModifierValue(NumericModifierDefinition modifier, double effectiveness) {
            Value operand = modifier.EvaluateAtLevel(1);
            if (modifier.Operation == NumericModifierOperation.Multiply) {
                double value = operand.ToDouble();
                double multiplier = 1d + (value - 1d) * effectiveness;
                return $"x{multiplier.ToString("0.###", CultureInfo.InvariantCulture)}";
            }
            Value effective = operand * Math.Max(0d, effectiveness);
            string suffix = modifier.Operation == NumericModifierOperation.AddPercent ? "%" : string.Empty;
            return $"+{effective}{suffix}";
        }

        private static string ResolveModifierLabel(BillRewardDefinition reward, NumericModifierDefinition modifier) {
            BillModifierPresentation[] entries = reward.ModifierPresentations;
            if (entries != null) {
                for (int index = 0; index < entries.Length; index++) {
                    if (string.Equals(entries[index].ModifierId, modifier.Id, StringComparison.Ordinal) &&
                        !string.IsNullOrWhiteSpace(entries[index].Label)) return entries[index].Label;
                }
            }
            return Humanize(modifier.ParameterId ?? modifier.Id ?? "Effect");
        }

        private static string ResolveModifierDescription(
            BillRewardDefinition reward,
            NumericModifierDefinition modifier) {
            BillModifierPresentation[] entries = reward.ModifierPresentations;
            if (entries != null) {
                for (int index = 0; index < entries.Length; index++) {
                    if (string.Equals(entries[index].ModifierId, modifier.Id, StringComparison.Ordinal)) {
                        return entries[index].Description ?? string.Empty;
                    }
                }
            }
            return string.IsNullOrWhiteSpace(modifier.ParameterGroupId)
                ? string.Empty
                : $"Buffs {Humanize(modifier.ParameterGroupId)} · {Humanize(modifier.ParameterId)}";
        }

        private static string Humanize(string value) {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var result = new StringBuilder(value.Length + 8);
            for (int index = 0; index < value.Length; index++) {
                char character = value[index];
                if (index > 0 && char.IsUpper(character) && !char.IsWhiteSpace(value[index - 1])) result.Append(' ');
                result.Append(character);
            }
            return result.ToString();
        }
    }
}
