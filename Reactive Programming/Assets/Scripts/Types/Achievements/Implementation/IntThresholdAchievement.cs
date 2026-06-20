using R3;
using Services.Statistics;

namespace Types.Modifiers.Definitions.Achievements.Implementation {
    public abstract class IntThresholdAchievement : AchievementItem {
        
        protected abstract int Target { get; }
        protected abstract StatisticKey<int> Key { get; }
        
        public IntThresholdAchievement(IStatisticsReader reader) : base(reader) {
        }

        protected override void StartTracking() {
            _statistics.GetObservable(Key)
                .Subscribe(OnValueChanged)
                .AddTo(_disposable);
        }

        private void OnValueChanged(int value) {
            var normalized = Target <= 0 ? 1f : (float) value / Target;
            ReportProgress(normalized, $"{value}/{Target}");

            if (value >= Target) {
                Complete();
            }
        }
    }
}