using R3;
using Types.Modifiers.Definitions.Achievements;
using UnityEngine;

namespace Views.Models {
    public class AchievementItemViewModel {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public Sprite Icon { get; }

        public ReadOnlyReactiveProperty<bool> IsCompleted { get; }
        public Observable<float> Progress { get; }
        public Observable<string> ProgressText { get; }

        public AchievementItemViewModel(IAchievement achievement, Sprite icon = null) {
            Id = achievement.Id;
            Name = achievement.Name;
            Description = achievement.Description;
            Icon = icon;

            IsCompleted = achievement.IsCompleted;
            Progress = achievement.Progress;
            ProgressText = achievement.ProgressText;
        }
    }
}