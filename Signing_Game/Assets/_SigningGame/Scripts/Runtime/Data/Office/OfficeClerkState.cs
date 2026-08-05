using System;

namespace Data.Office {
    public sealed class OfficeClerkState {
        public int Id { get; }
        public double IncomeMultiplier { get; }
        public float Progress { get; internal set; }

        internal OfficeClerkState(int id, double incomeMultiplier, float progress = 0f) {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (double.IsNaN(incomeMultiplier) || double.IsInfinity(incomeMultiplier) || incomeMultiplier < 0d) {
                throw new ArgumentOutOfRangeException(nameof(incomeMultiplier));
            }

            Id = id;
            IncomeMultiplier = incomeMultiplier;
            Progress = progress;
        }
    }
}
