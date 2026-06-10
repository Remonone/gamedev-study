using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

namespace Views.Models {
    [Serializable]
    public struct AchievementViewData {
        public string Id;
        public string Name;
        public string Description;
        public string Reward;
        public Sprite Icon;
        public bool IsCompleted;
    }

    public class AchievementsTabViewModel {
        public ReactiveProperty<IReadOnlyList<AchievementViewData>> Achievements = new(Array.Empty<AchievementViewData>());

        public void RequestInitialState() {
        }

        public void OnAchievementClicked(string achievementId) {
        }
    }
}
