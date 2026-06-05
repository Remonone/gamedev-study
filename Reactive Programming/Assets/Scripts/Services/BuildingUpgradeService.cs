using UnityEngine;

namespace Services {
    public class BuildingUpgradeService : IService {
        
        private readonly InvalidationService _invalidationService;
        private readonly BuildingWatcherService _buildingWatcherService;
        
        public BuildingUpgradeService(InvalidationService invalidationService, BuildingWatcherService buildingWatcherService) {
            _invalidationService = invalidationService;
            _buildingWatcherService = buildingWatcherService;
        }
        
        public void UpgradeBuilding(string name, int levels) {
            var building = _buildingWatcherService.GetBuildingState(name);
            if (building == null) return;
            building.Level += levels;
            building.LastTimeActivated = Time.timeAsDouble;
            _invalidationService.InvalidateBuilding(name);
        }
    }
}