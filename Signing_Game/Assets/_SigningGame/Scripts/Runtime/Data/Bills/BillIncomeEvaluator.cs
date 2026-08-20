using System;
using System.Collections.Generic;
using Data.Cache;
using Data.Modifiers;

namespace Data.Bills {
    public static class BillIncomeEvaluator {
        public static IncomeEntries Evaluate(
            IncomeEntries baseline,
            IReadOnlyList<BillCompletionRecord> completions,
            IReadOnlyList<ActiveBillState> active,
            BillEntries billEntries) {
            IncomeEntries result = BillCompletionModifierEvaluator.Apply(
                baseline,
                completions,
                billEntries);
            if (active == null || active.Count == 0) return result;

            double baseMultiplier = Math.Clamp(billEntries.BaseActiveIncomeMultiplier, 0d, 1d);
            double penaltyStrength = Math.Clamp(billEntries.ActiveIncomePenaltyStrength, 0d, 1d);
            double multiplier = 1d - (1d - baseMultiplier) * penaltyStrength;
            result.IncomePerDocument *= Math.Max(0d, multiplier);
            return result;
        }
    }
}
