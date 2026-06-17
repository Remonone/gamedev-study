using System.Collections.Generic;
using System.Linq;
using Audio.Implementation;
using Types.Enums.Buildings;
using Types.Enums.Objects;
using Components.Instances;
using Economy.Providers;
using R3;
using Services.Player;
using Services;
using Services.Achievements;
using Services.Statistics;
using Types.Enums;
using UnityEngine;
using UnityEngine.UIElements;
using Views;
using Views.Models;

namespace Components {
    public class GameServiceInstaller : ServiceInstaller {
        
        [SerializeField] private WorldCastService _worldCastService;
        [SerializeField] private UIDocument _document;
        [SerializeField] private StructureSoundConfig _structureSoundConfig;
        [SerializeField] private BuildingItemView _buildingItemView;
        [SerializeField] private ControlsView _controlsView;
        
        private BuildingWatcherService _buildingWatcherService;
        private EconomyService _economyService;
        private StatisticsService _statisticsService;
        private NotificationService _notificationService;
        private AchievementService _achievementService;
        
        protected override void InstallServices() {
            var storage = new Storage();
            var sessionContext = new SessionContext();
            var providerRegistry = new ProviderRegistryService();
            var unlockService = new UnlockService();
            var stateBenefitCalculation = new StateBenefitCalculationService(sessionContext);
            _notificationService = new NotificationService();
            UploadProviders(providerRegistry);
            
            RegisterService(_notificationService);
            RegisterService(storage);
            RegisterService(_worldCastService);
            RegisterService(providerRegistry);
            RegisterService(unlockService);
            RegisterService(stateBenefitCalculation);
            
            RegisterService(new StructureClickService(storage, _worldCastService, unlockService));
            RegisterService(new StructureSoundResolver(_structureSoundConfig));
            
            var buildingDefinitions = FetchBuildingDefinitions();
            _buildingWatcherService = new BuildingWatcherService(buildingDefinitions);
            RegisterService(_buildingWatcherService);
            
            var invalidationService = new InvalidationService(_buildingWatcherService.BuildingsByName);
            RegisterService(invalidationService);
            
            RegisterService(new UpgradeService(storage, providerRegistry, invalidationService, unlockService));
            var buildingUpgradeService = new BuildingUpgradeService(invalidationService, _buildingWatcherService);
            RegisterService(buildingUpgradeService);

            _economyService = new EconomyService(sessionContext, storage, _buildingWatcherService, buildingUpgradeService, providerRegistry);
            RegisterService(_economyService);
            
            var tickService = new TickService(_economyService, _buildingWatcherService, storage, stateBenefitCalculation);
            RegisterService(tickService);

            var saveService = new SaveService(SaveManager);
            RegisterService(saveService);
            
            InitStatistics();
            InitAchievements();
            InitTrackers();
            InitViews();
            BindNotifications();
        }

        private void BindNotifications() {
            _achievementService.Unlocked
                .Subscribe(achievement => _notificationService.Push(new NotificationRequest(
                    title: "Achievement unlocked!",
                    message: achievement.Name,
                    type: NotificationType.Achievement
                ))).AddTo(this);
        }

        private void InitAchievements() {
            var achievements = AchievementsCollector.Collect(_statisticsService);
            _achievementService = new AchievementService(achievements);
            RegisterService(_achievementService);
            _achievementService.Start();
        }

        private void InitStatistics() {
            _statisticsService = new StatisticsService();
            StatisticsRegistry.RegisterStatistics(_statisticsService);
            RegisterService(_statisticsService);
        }

        private void InitTrackers() {
            var trackers = TrackersCollector.Collect(_statisticsService);
            var trackerService = new StatisticsTrackingService(trackers);
            trackerService.Start();
            RegisterService(trackerService);
        }

        private void UploadProviders(ProviderRegistryService providerRegistry) {
            providerRegistry.RegisterProvider(new UpgradeModifierProvider());
        }

        private List<BuildingDefinition> FetchBuildingDefinitions() {
            return Resources.LoadAll<BuildingDefinition>("Buildings").ToList();
        }

        private void InitViews() {
            var controls = new Controls();
            _controlsView.Bind(controls);

            BindNotifications(controls);
            
            var container = _document.rootVisualElement.Q<VisualElement>("BuildingList");
            foreach (var building in _buildingWatcherService.BuildingsByName.Values) {
                var buildingItem = Instantiate(_buildingItemView);
                var buildingItemViewModel = new BuildingItemViewModel(building.Definition);
                buildingItem.Bind(buildingItemViewModel, container);
                _economyService.ComputeStatsForBuilding(building);
            }
            
            
        }

        private void BindNotifications(Controls controls) {
            var notificationContainer = _document.rootVisualElement.Q<VisualElement>("NotificationCenter");
            var notificationTemplate = Resources.Load<VisualTreeAsset>("UI/NotificationItem");
            var notificationView = new NotificationCenterView(notificationContainer, notificationTemplate);
            var notificationVm = new NotificationCenterViewModel(_notificationService);
            notificationView.Bind(notificationVm);
        }

        protected override void AfterInstallation() { }
    }
}
