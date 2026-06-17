using System.Collections.Generic;
using Types.Enums.Buildings;
using Economy;
using Services.Player;
using R3;
using Types.Enums;

namespace Services {
    public class EconomyService : IService {
        private readonly ISessionContext _sessionContext;
        private readonly StatResolver _statResolver;
        private readonly BuildingUpgradeService _buildingUpgradeService;
        private readonly BuildingWatcherService _buildingWatcherService;
        private readonly Storage _storage;
        private readonly ProviderRegistryService _providerRegistryService;
        
        private Subject<BuildingUpdate> _buildingUpdate = new();
        
        public Observable<BuildingUpdate> BuildingUpdate => _buildingUpdate;
        
        public EconomyService(ISessionContext sessionContext, Storage storage, BuildingWatcherService watcherService, BuildingUpgradeService buildingUpgradeService, ProviderRegistryService providerRegistryService) {
            _sessionContext = sessionContext;
            _buildingWatcherService = watcherService;
            _buildingUpgradeService = buildingUpgradeService;
            _providerRegistryService = providerRegistryService;
            _storage = storage;
            _statResolver = new StatResolver();
        }

        public ComputedStats ComputeStatsForBuilding(BuildingState building) {
            if(!building.IsDirty) return building.Cache;
            var modifiers = new List<StatModifier>();
            _providerRegistryService.FetchModifiers(_sessionContext, building, modifiers);
            building.Cache = _statResolver.Resolve(building, modifiers);
            building.IsDirty = false;
            _buildingUpdate.OnNext(new BuildingUpdate(building, building.Cache));
            return building.Cache;
        }

        public void PurchaseBuilding(string name) {
            var building = _buildingWatcherService.GetBuildingState(name);
            if (!ValidateCost(building)) return;
            _storage.Spend(building.Cache.Cost);
            _buildingUpgradeService.UpgradeBuilding(name, 1);
        }

        private bool ValidateCost(BuildingState building) {
            if (building == null) return false;
            return _storage.CanAfford(building.Cache.Cost);
        }
    }
}
