using System;
using Economy.Providers;
using R3;
using Types.Achievements;

namespace Services.Achievements {
    public class AchievementTrackerService : IService, IDisposable, IStartable {
        
        private readonly AchievementService _achievementService;
        private readonly AchievementStorageService _achievementStorageService;
        private readonly InvalidationService _invalidationService;
        private readonly AchievementModifierProvider _achievementModifierProvider;
        
        private CompositeDisposable _disposable;
        
        public AchievementTrackerService(AchievementService achievementService, 
            AchievementStorageService achievementStorageService, 
            ProviderRegistryService providerRegistryService,
            InvalidationService invalidationService) {
            _achievementService = achievementService;
            _achievementStorageService = achievementStorageService;
            
            _achievementModifierProvider = providerRegistryService.GetProvider<AchievementModifierProvider>();
            
            _disposable = new CompositeDisposable();
            _invalidationService = invalidationService;
        }

        public void OnUnlocked(IAchievement achievement) {
            var achievementModifier = _achievementStorageService.GetAchievementModifier(achievement.Id);
            if (achievementModifier == null) return;
            
            _achievementModifierProvider.AddAchievementModifier(achievementModifier);
            foreach (var modifier in achievementModifier.Modifiers) {
                _invalidationService.MarkDirtyByTarget(modifier.Target);
            }
        }

        public void Dispose() {
            _disposable?.Dispose();
        }

        public void StartService() {
            _achievementService.Unlocked
                .Subscribe(OnUnlocked).AddTo(_disposable);
        }
    }
}