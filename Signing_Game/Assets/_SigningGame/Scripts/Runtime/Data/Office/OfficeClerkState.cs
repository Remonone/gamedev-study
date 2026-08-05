using System;
using Utils;

namespace Data.Office {
    public sealed class OfficeClerkState {
        public int Id { get; }
        public string Name { get; }
        public int Age { get; }
        public Value OriginalHirePrice { get; }
        public double BaseEfficiency { get; }
        public double BonusEfficiency { get; private set; }
        public double IncomeMultiplier { get; private set; }
        public float Progress { get; internal set; }

        internal OfficeClerkState(
            int id,
            string name,
            int age,
            Value originalHirePrice,
            double baseEfficiency,
            double bonusEfficiency,
            float progress = 0f) {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (string.IsNullOrWhiteSpace(name) || name.Length > 64) throw new ArgumentOutOfRangeException(nameof(name));
            if (age < 18 || age > 65) throw new ArgumentOutOfRangeException(nameof(age));
            if (originalHirePrice.IsZero) throw new ArgumentOutOfRangeException(nameof(originalHirePrice));
            ValidateEfficiency(baseEfficiency, nameof(baseEfficiency));
            ValidateEfficiency(bonusEfficiency, nameof(bonusEfficiency));

            Id = id;
            Name = name;
            Age = age;
            OriginalHirePrice = originalHirePrice;
            BaseEfficiency = baseEfficiency;
            BonusEfficiency = bonusEfficiency;
            IncomeMultiplier = CalculateIncomeMultiplier(baseEfficiency, bonusEfficiency);
            Progress = progress;
        }

        internal void SetBonusEfficiency(double bonusEfficiency) {
            ValidateEfficiency(bonusEfficiency, nameof(bonusEfficiency));
            BonusEfficiency = bonusEfficiency;
            IncomeMultiplier = CalculateIncomeMultiplier(BaseEfficiency, bonusEfficiency);
        }

        private static double CalculateIncomeMultiplier(double baseEfficiency, double bonusEfficiency) {
            double multiplier = bonusEfficiency >= double.MaxValue - 1d
                ? double.MaxValue
                : 1d + bonusEfficiency;
            if (baseEfficiency <= 0d || multiplier <= 0d) return 0d;
            return baseEfficiency >= double.MaxValue / multiplier
                ? double.MaxValue
                : baseEfficiency * multiplier;
        }

        private static void ValidateEfficiency(double value, string parameterName) {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d) {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
