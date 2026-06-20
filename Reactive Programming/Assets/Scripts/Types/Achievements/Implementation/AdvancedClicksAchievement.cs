using Services.Statistics;

namespace Types.Modifiers.Definitions.Achievements.Implementation {
    public class AdvancedClicksAchievement : IntThresholdAchievement {
        public AdvancedClicksAchievement(IStatisticsReader reader) : base(reader) {
        }

        public override string Id => "total_clicks_100000";
        public override string Name => "Click Pro";
        public override string Description => "Click 100000 times";
        protected override int Target => 100000;
        protected override StatisticKey<int> Key => StatisticKeys.TotalClicks;
    }
}