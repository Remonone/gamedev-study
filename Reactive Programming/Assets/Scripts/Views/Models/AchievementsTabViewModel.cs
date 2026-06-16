using System;
using System.Collections.Generic;
using System.Linq;
using R3;
using Services.Achievements;
using UnityEngine;

namespace Views.Models {

    public class AchievementsTabViewModel {
        public ReactiveProperty<IReadOnlyList<AchievementItemViewModel>> Achievements =
            new(Array.Empty<AchievementItemViewModel>());

        private readonly AchievementService _achievementService;

        public AchievementsTabViewModel(AchievementService achievementService) {
            _achievementService = achievementService;
        }

        public void RequestInitialState() {
            var items = _achievementService.Achievements
                .Select(a => new AchievementItemViewModel(a))
                .ToList();
            Achievements.Value = items;
        }

        public void OnAchievementClicked(string achievementId) {
            // TODO: Add logic to get reward
        }
    }
}
