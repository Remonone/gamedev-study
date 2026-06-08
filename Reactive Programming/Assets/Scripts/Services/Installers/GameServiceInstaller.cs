using System.Collections.Generic;
using System.Linq;
using Audio.Implementation;
using Bases.Buildings;
using Bases.Objects;
using Components.Instances;
using Player;
using Services;
using Types.Economy;
using UnityEngine;
using UnityEngine.Serialization;
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
        
        protected override void InstallServices() {
            var storage = new Storage();

            var sessionContext = new SessionContext();

            var buildingDefinitions = FetchBuildingDefinitions();
            
            RegisterService(storage);
            RegisterService(_worldCastService);
            RegisterService(new StructureClickService(storage, _worldCastService));
            RegisterService(new StructureSoundResolver(_structureSoundConfig));
            
            _buildingWatcherService = new BuildingWatcherService(buildingDefinitions);
            RegisterService(_buildingWatcherService);
            
            var invalidationService = new InvalidationService(_buildingWatcherService.BuildingsByName);
            RegisterService(invalidationService);
            
            var buildingUpgradeService = new BuildingUpgradeService(invalidationService, _buildingWatcherService);
            RegisterService(buildingUpgradeService);

            _economyService = new EconomyService(sessionContext, storage, _buildingWatcherService, buildingUpgradeService);
            RegisterService(_economyService);
            
            var tickService = new TickService(_economyService, _buildingWatcherService, storage);
            RegisterService(tickService);

            var saveService = new SaveService(SaveManager);
            RegisterService(saveService);
            
            InitViews();
        }

        private List<BuildingDefinition> FetchBuildingDefinitions() {
            return Resources.LoadAll<BuildingDefinition>("Buildings").ToList();
        }

        private void InitViews() {
            var controls = new Controls();
            _controlsView.Bind(controls);
            
            var container = _document.rootVisualElement.Q<VisualElement>("BuildingList");
            foreach (var building in _buildingWatcherService.BuildingsByName.Values) {
                var buildingItem = Instantiate(_buildingItemView);
                var buildingItemViewModel = new BuildingItemViewModel(building.Definition);
                buildingItem.Bind(buildingItemViewModel, container);
                _economyService.ComputeStatsForBuilding(building);
            }
        }

        protected override void AfterInstallation() { }
    }
}