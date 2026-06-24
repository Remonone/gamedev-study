using System.Collections.Generic;
using System.Linq;
using Types.Achievements;

namespace Services.Achievements {
    public class AchievementStorageService : IService {
        private Dictionary<string, AchievementModifier> _achievementModifiers;

        public AchievementStorageService(IReadOnlyList<AchievementModifier> achievementModifiers) {
            _achievementModifiers = achievementModifiers.ToDictionary(modifier => modifier.TrackedAchievement);
        }
        
        public AchievementModifier GetAchievementModifier(string trackedAchievement) {
            return _achievementModifiers.TryGetValue(trackedAchievement, out var modifier) ? modifier : null;
        }
    }
}