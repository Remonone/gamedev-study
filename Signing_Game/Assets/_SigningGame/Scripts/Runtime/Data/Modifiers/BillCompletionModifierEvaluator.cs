using System;
using System.Collections.Generic;
using Data.Bills;
using Data.Cache;
using Data.Modifiers.Calculation;

namespace Data.Modifiers {
    public static class BillCompletionModifierEvaluator {
        public static T Apply<T>(
            T target,
            IReadOnlyList<BillCompletionRecord> completions,
            BillEntries billEntries) where T : struct {
            if (typeof(T) == typeof(BillEntries) || completions == null) return target;

            T result = target;
            for (int completionIndex = 0; completionIndex < completions.Count; completionIndex++) {
                result = ApplySingle(result, completions[completionIndex], billEntries);
            }

            return result;
        }

        public static T ApplySingle<T>(T target, BillCompletionRecord completion, BillEntries billEntries)
            where T : struct {
            if (typeof(T) == typeof(BillEntries) || completion == null) return target;
            double effectiveness = SaturatingMultiplyPositive(
                completion.SavedBaseRewardStrength,
                billEntries.OverallRewardMultiplier);
            if (effectiveness <= 0d) return target;

            var context = new ModifierContext()
                .Add(new LevelModifierCapability(1))
                .Add(new ModifierEffectivenessCapability((float)Math.Min(effectiveness, float.MaxValue)));
            ModifierDefinition[] definitions = completion.Reward.CompletionModifiers;
            if (definitions == null) return target;

            T result = target;
            for (int definitionIndex = 0; definitionIndex < definitions.Length; definitionIndex++) {
                ModifierDefinition definition = definitions[definitionIndex];
                if (definition?.NumericModifiers == null) continue;
                for (int modifierIndex = 0; modifierIndex < definition.NumericModifiers.Count; modifierIndex++) {
                    NumericModifierDefinition modifier = definition.NumericModifiers[modifierIndex];
                    if (modifier == null || !modifier.IsApplicable(result)) continue;
                    if (modifier.Operation == NumericModifierOperation.Override) {
                        throw new InvalidOperationException(
                            $"Bill reward '{completion.Reward.Id}' contains an unsupported Override modifier.");
                    }
                    result = modifier.Apply(result, context, true);
                }
            }
            return result;
        }

        private static double SaturatingMultiplyPositive(double left, double right) {
            if (double.IsNaN(left) || double.IsNaN(right) || left <= 0d || right <= 0d) return 0d;
            if (double.IsPositiveInfinity(left) || double.IsPositiveInfinity(right) ||
                left > double.MaxValue / right) return double.MaxValue;
            return left * right;
        }
    }
}
