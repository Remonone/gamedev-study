using System.Collections.Generic;
using System.Linq;
using Audio.Implementation;
using Types.Buildings;
using Types.Objects;
using Economy.Providers;
using R3;
using Services.Player;
using Services.Achievements;
using Services.Components.Instances;
using Services.Events;
using Services.Gamerule;
using Services.QTE;
using Services.Statistics;
using Types;
using Types.Events.Global;
using Types.Modifiers;
using Types.Practices;
using Types.QTE;
using Types.Achievements;
using Types.Research;
using UnityEngine;
using UnityEngine.UIElements;
using Views;
using Views.Models;

namespace Services.Components {
    public class GameServiceInstaller : ServiceInstaller {
        
        [SerializeField, Tooltip("Scene service that converts player clicks into structure selections.")]
        private WorldCastService _worldCastService;
        [SerializeField, Tooltip("Main UI document containing the building list and notification center.")]
        private UIDocument _document;
        [SerializeField, Tooltip("Sound mapping used by structure click audio.")]
        private StructureSoundConfig _structureSoundConfig;
        [SerializeField, Tooltip("Building card prefab instantiated for each building definition.")]
        private BuildingItemView _buildingItemView;
        [SerializeField, Tooltip("Controls view bound to the main UI model.")]
        private ControlsView _controlsView;
        [SerializeField, Min(0f), Tooltip("Minutes between global events. Duration is configured per event in seconds.")]
        private float _globalEventIntervalMinutes = 1f;
        [SerializeField, Tooltip("QTE spawn/lifetime/reward configuration. Falls back to Resources/QTE/QteConfig when empty.")]
        private QteConfig _qteConfig;
        
        private BuildingWatcherService _buildingWatcherService;
        private EconomyService _economyService;
        private StatisticsService _statisticsService;
        private NotificationService _notificationService;
        private AchievementService _achievementService;

        private Storage _storage;
        
        protected override void InstallServices() {
            _storage = new Storage();
            var sessionContext = new SessionContext();
            var providerRegistry = new ProviderRegistryService();
            var unlockService = new UnlockService();
            var stateBenefitCalculation = new StateBenefitCalculationService(sessionContext);
            _notificationService = new NotificationService();
            UploadProviders(providerRegistry);
            
            RegisterService(_notificationService);
            RegisterService(_storage);
            RegisterService(_worldCastService);
            RegisterService(providerRegistry);
            RegisterService(unlockService);
            RegisterService(stateBenefitCalculation);

            var gameRuleService = new GameRuleService(GetInitialGameRules());
            RegisterService(gameRuleService);
            
            var buildingDefinitions = FetchBuildingDefinitions();
            _buildingWatcherService = new BuildingWatcherService(buildingDefinitions);
            RegisterService(_buildingWatcherService);
            
            var invalidationService = new InvalidationService(_buildingWatcherService.BuildingsByName);
            RegisterService(invalidationService);

            var practiceRewardConfig = FetchPracticeRewardConfig();
            
            var practiceService = new PracticeService(
                FetchPractices(),
                practiceRewardConfig,
                invalidationService,
                sessionContext);
            RegisterService(practiceService);
            providerRegistry.RegisterProvider(new PracticesModifierProvider(practiceService));
            
            var recycleService = new RecycleService(practiceService, _buildingWatcherService, practiceRewardConfig);
            RegisterService(recycleService);
            providerRegistry.RegisterProvider(new RecycleModifierProvider(recycleService));
            
            var globalEventService = new GlobalEventService(FetchGlobalEvents(), invalidationService, _globalEventIntervalMinutes);
            RegisterService(globalEventService);
            providerRegistry.RegisterProvider(new GlobalEventModifierProvider(globalEventService));
            
            var upgradeService = new UpgradeService(_storage, 
                providerRegistry, 
                invalidationService, 
                unlockService);
            RegisterService(upgradeService);

            var buildingUpgradeService = new BuildingUpgradeService(invalidationService, _buildingWatcherService);
            RegisterService(buildingUpgradeService);

            _economyService = new EconomyService(sessionContext, 
                _storage, 
                _buildingWatcherService, 
                buildingUpgradeService, 
                providerRegistry);
            RegisterService(_economyService);
            
            var structureClickService = new StructureClickService(_storage, 
                _worldCastService, 
                unlockService, 
                _economyService, 
                stateBenefitCalculation);
            RegisterService(structureClickService);
            RegisterService(new StructureSoundResolver(structureClickService, _structureSoundConfig));
            RegisterService(new PlayerEffectService(sessionContext, buildingUpgradeService, invalidationService));
            RegisterService(new ResearchService(
                sessionContext,
                unlockService,
                _notificationService,
                practiceService,
                FetchResearchConfig()));
            
            var tickService = new TickService(_economyService, 
                _buildingWatcherService, 
                _storage, 
                stateBenefitCalculation);
            RegisterService(tickService);

            var saveService = new SaveService(SaveManager);
            RegisterService(saveService);
            
            InitStatistics();
            InitAchievements();
            InitTrackers();
            InitQteService(practiceService, upgradeService);
            
            var achievements = Resources.LoadAll<AchievementModifier>("Achievements");
            var achievementStorage = new AchievementStorageService(achievements);
            RegisterService(achievementStorage);
            RegisterService(new AchievementTrackerService(_achievementService, 
                                                            achievementStorage, 
                                                            providerRegistry, 
                                                            invalidationService));
            
            InitViews();
        }

        private IReadOnlyDictionary<string, object> GetInitialGameRules() {
            return new Dictionary<string, object> {
            };
        }

        private void InitAchievements() {
            var achievements = AchievementsCollector.Collect(_statisticsService);
            _achievementService = new AchievementService(achievements);
            RegisterService(_achievementService);
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
            providerRegistry.RegisterProvider(new AchievementModifierProvider());
            providerRegistry.RegisterProvider(new StateModifierProvider());
        }

        private List<BuildingDefinition> FetchBuildingDefinitions() {
            return Resources.LoadAll<BuildingDefinition>("Buildings").ToList();
        }

        private List<GlobalEvent> FetchGlobalEvents() {
            return Resources.LoadAll<GlobalEvent>("GlobalEvents").ToList();
        }

        private ResearchConfig FetchResearchConfig() {
            return Resources.Load<ResearchConfig>("Research/ResearchConfig");
        }

        private List<Practice> FetchPractices() {
            return Resources.LoadAll<Practice>("Practices").ToList();
        }

        private PracticeRewardConfig FetchPracticeRewardConfig() {
            return Resources.Load<PracticeRewardConfig>("Practices/PracticeRewardConfig");
        }

        private QteConfig FetchQteConfig() {
            return _qteConfig != null ? _qteConfig : Resources.Load<QteConfig>("QTE/QteConfig");
        }

        private void InitQteService(PracticeService practiceService, UpgradeService upgradeService) {
            var config = FetchQteConfig();
            if (config == null) {
                Debug.LogWarning("QTE service was not registered: QTE config is missing.");
                return;
            }

            if (config.Prefab == null) {
                Debug.LogWarning("QTE service was not registered: QTE prefab is missing in config.");
                return;
            }

            var aggregator = new QteModifierAggregator(practiceService, upgradeService);
            RegisterService(aggregator);

            var rewardService = new QteRewardService(_statisticsService, _storage);
            RegisterService(rewardService);

            RegisterService(new QteService(config, rewardService, aggregator, _worldCastService));
        }

        private void InitViews() {
            var controls = new Controls(_storage);
            _controlsView.Bind(controls);

            BindNotifications();
            
            var container = _document.rootVisualElement.Q<VisualElement>("BuildingList");
            foreach (var building in _buildingWatcherService.BuildingsByName.Values) {
                if (!building.Definition.IsUpgradeable) continue;
                var buildingItem = Instantiate(_buildingItemView);
                var buildingItemViewModel = new BuildingItemViewModel(building.Definition);
                buildingItem.Bind(buildingItemViewModel, container);
            }
            
            
        }
        private void BindNotifications() {
            var notificationContainer = _document.rootVisualElement.Q<VisualElement>("NotificationCenter");
            var notificationTemplate = Resources.Load<VisualTreeAsset>("UI/NotificationItem");
            var notificationView = new NotificationCenterView(notificationContainer, notificationTemplate);
            var notificationVm = new NotificationCenterViewModel(_notificationService);
            notificationView.Bind(notificationVm);
        }

        protected override void AfterInstallation() {
            _achievementService.Unlocked
                .Where(achievement => !achievement.IsLoadedAsCompleted)
                .Subscribe(achievement => _notificationService.Push(new NotificationRequest(
                    title: "Achievement unlocked!",
                    message: achievement.Name,
                    type: NotificationType.Achievement
                ))).AddTo(this);
        }
    }
}
