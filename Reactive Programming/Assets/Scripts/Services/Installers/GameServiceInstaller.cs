using System.Collections.Generic;
using Audio.Implementation;
using Bases.Buildings;
using Bases.Objects;
using Components.Instances;
using Economy;
using Player;
using Services;
using UnityEngine;
using UnityEngine.Serialization;
using Views;
using Views.Models;

namespace Components {
    public class GameServiceInstaller : ServiceInstaller {
        
        [SerializeField] private AreaWatcherView _areaWatcherView;
        [SerializeField] private StructureSoundConfig _structureSoundConfig;
        [SerializeField] private List<BuildingDefinition> _buildingDefinitions;
        
        protected override void InstallServices() {
            var storage = new Storage();
            ServiceLocator.Instance.RegisterService(storage);
            ServiceLocator.Instance.RegisterService(new StructureClickService(storage));
            ServiceLocator.Instance.RegisterService(new StructureSoundResolver(_structureSoundConfig));
            ServiceLocator.Instance.RegisterService(new StatResolver());
            
            var buildingWatcherService = new BuildingWatcherService(_buildingDefinitions);
            ServiceLocator.Instance.RegisterService(buildingWatcherService);
            
            var invalidationService = new InvalidationService(buildingWatcherService.BuildingsByName);
            ServiceLocator.Instance.RegisterService(invalidationService);
            
            var buildingUpgradeService = new BuildingUpgradeService(invalidationService, buildingWatcherService);
            ServiceLocator.Instance.RegisterService(buildingUpgradeService);
            
            InitViews();
        }

        private void InitViews() {
            var areaClickerViewModel = new AreaClickerViewModel();
            _areaWatcherView.Init(areaClickerViewModel);
        }
    }
}