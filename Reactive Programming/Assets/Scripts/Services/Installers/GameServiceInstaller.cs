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
using UnityEngine.UIElements;
using Views;
using Views.Models;

namespace Components {
    public class GameServiceInstaller : ServiceInstaller {
        
        [SerializeField] private AreaWatcherView _areaWatcherView;
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
            
            ServiceLocator.Instance.RegisterService(storage);
            ServiceLocator.Instance.RegisterService(new StructureClickService(storage));
            ServiceLocator.Instance.RegisterService(new StructureSoundResolver(_structureSoundConfig));
            
            _buildingWatcherService = new BuildingWatcherService(buildingDefinitions);
            ServiceLocator.Instance.RegisterService(_buildingWatcherService);
            
            var invalidationService = new InvalidationService(_buildingWatcherService.BuildingsByName);
            ServiceLocator.Instance.RegisterService(invalidationService);
            
            var buildingUpgradeService = new BuildingUpgradeService(invalidationService, _buildingWatcherService);
            ServiceLocator.Instance.RegisterService(buildingUpgradeService);

            _economyService = new EconomyService(sessionContext, storage, _buildingWatcherService, buildingUpgradeService);
            ServiceLocator.Instance.RegisterService(_economyService);
            
            var tickService = new TickService(_economyService, _buildingWatcherService, storage);
            ServiceLocator.Instance.RegisterService(tickService);
            
            InitViews();
        }

        private List<BuildingDefinition> FetchBuildingDefinitions() {
            return Resources.LoadAll<BuildingDefinition>("Buildings").ToList();
        }

        private void InitViews() {
            var areaClickerViewModel = new AreaClickerViewModel();
            _areaWatcherView.Init(areaClickerViewModel);
            
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
    }
}