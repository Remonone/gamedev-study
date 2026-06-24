using Services.Statistics;

namespace Types.Achievements.Implementation {
    public class TotalClicksAchievement : IntThresholdAchievement {
        private const int _Target = 1000;


        public TotalClicksAchievement(IStatisticsReader reader) : base(reader) {
        }

        public override string Id => "total_clicks_1000";
        public override string Name => "Click Amateur";
        public override string Description => "Click 1000 times";

        protected override StatisticKey<int> Key => StatisticKeys.TotalClicks;
        protected override int Target => _Target;
    }
}