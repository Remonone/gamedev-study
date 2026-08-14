using System;
using System.Collections.Generic;
using Data.Upgrades;
using Utils;

namespace Services {
    public static class MetaPointCalculator {
        public static long FromMoneyPeak(Value peak) {
            if (peak.IsZero) return 0L;

            // The design example treats 92.2 as contributing two mantissa digits.
            double mantissaPoints = Math.Floor(Math.Log10(peak.Stored)) + 1d;
            long degreePoints = Math.Max(0L, (long)peak.Base.Degree - 1L) * 3L;
            double total = mantissaPoints + degreePoints;
            if (total <= 0d) return 0L;
            return total >= long.MaxValue ? long.MaxValue : (long)total;
        }

        public static Value ToValue(long points) {
            return points <= 0L ? Value.Zero : new Value(points);
        }

        public static long Calculate(long markedUpgradeLevels, Value peak) {
            if (markedUpgradeLevels < 0L) throw new ArgumentOutOfRangeException(nameof(markedUpgradeLevels));
            long money = FromMoneyPeak(peak);
            return markedUpgradeLevels > long.MaxValue - money ? long.MaxValue : markedUpgradeLevels + money;
        }

        public static bool IsEligible(long currentPoints, long previousIterationPoints, long threshold = 5L) {
            return currentPoints >= threshold && currentPoints > previousIterationPoints;
        }

        public static long CountMarkedLevels(IEnumerable<UpgradeNodeState> upgrades) {
            if (upgrades == null) return 0L;
            long result = 0L;
            foreach (UpgradeNodeState upgrade in upgrades) {
                if (upgrade?.Definition == null || !upgrade.Definition.GrantsMetaCurrencyPoint || upgrade.Level <= 0) continue;
                if (result > long.MaxValue - upgrade.Level) return long.MaxValue;
                result += upgrade.Level;
            }
            return result;
        }
    }
}
