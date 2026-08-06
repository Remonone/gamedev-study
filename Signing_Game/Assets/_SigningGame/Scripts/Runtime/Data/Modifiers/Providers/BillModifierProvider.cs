using System;
using Data.Cache;
using Services;
using Services.Locator;

namespace Data.Modifiers.Providers {
    public sealed class BillModifierProvider : IModifierProvider {
        private BillService _bills;
        private IReadOnlyCacheData<BillEntries> _billData;

        public T Collect<T>(T target) where T : struct {
            if (typeof(T) == typeof(BillEntries) || _bills == null) return target;

            BillEntries entries = _billData.Value;
            T result = BillCompletionModifierEvaluator.Apply(target, _bills.CompletionRecords, entries);
            if (!_bills.HasActiveBills) return result;

            if (result is IncomeEntries income) {
                double baseMultiplier = Math.Clamp(entries.BaseActiveIncomeMultiplier, 0d, 1d);
                double penaltyStrength = Math.Clamp(entries.ActiveIncomePenaltyStrength, 0d, 1d);
                double multiplier = 1d - (1d - baseMultiplier) * penaltyStrength;
                income.IncomePerDocument *= multiplier;
                return (T)(object)income;
            }

            if (result is GenerationEntries generation) {
                double bonus = _bills.GetStrongestActiveGenerationBonus(entries);
                if (bonus > 0d) generation.TokenPerSecond = SaturatingFloat(
                    generation.TokenPerSecond * (1d + bonus));
                return (T)(object)generation;
            }

            return result;
        }

        public void Init(IServiceScope scope) {
            _bills = scope.Get<BillService>();
            _billData = scope.Get<PlayerStatStash>().BillData;
        }

        private static float SaturatingFloat(double value) {
            if (double.IsNaN(value) || value <= 0d) return 0f;
            if (double.IsPositiveInfinity(value) || value >= float.MaxValue) return float.MaxValue;
            return (float)value;
        }
    }
}
