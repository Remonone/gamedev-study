using Types.Achievements;
using Types.Achievements.Implementation;

namespace Services.Statistics {
    public static class AchievementsCollector {
        public static AchievementItem[] Collect(IStatisticsReader reader) {
            return new AchievementItem[] {
                new TotalClicksAchievement(reader),
                new AdvancedClicksAchievement(reader)
            };
        }
    }
}