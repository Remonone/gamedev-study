using System;
using System.Collections.Generic;
using Bases.Buildings;
using Economy;
using Player;
using R3;
using Types.Economy;

namespace Services {
    public class EconomyService : IService {
        private readonly SessionContext _sessionContext;
        private readonly StatResolver _statResolver;
        private readonly BuildingUpgradeService _buildingUpgradeService;
        private readonly BuildingWatcherService _buildingWatcherService;
        private readonly Storage _storage;
        
        private readonly Dictionary<string, List<StatModifier>> _statModifiers;
        
        private Subject<BuildingUpdate> _buildingUpdate = new();
        public Observable<BuildingUpdate> BuildingUpdate => _buildingUpdate;
        
        public EconomyService(SessionContext sessionContext, Storage storage, BuildingWatcherService watcherService, BuildingUpgradeService buildingUpgradeService) {
            _sessionContext = sessionContext;
            _buildingWatcherService = watcherService;
            _buildingUpgradeService = buildingUpgradeService;
            _storage = storage;
            _statResolver = new StatResolver();
            _statModifiers = new Dictionary<string, List<StatModifier>>();
        }

        public ComputedStats ComputeStatsForBuilding(BuildingState building) {
            if(!building.IsDirty) return building.Cache;
            var name = building.Definition.Name;
            VerifyName(name);
            building.Cache = _statResolver.Resolve(building, _statModifiers[name]);
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

        private void VerifyName(string name) {
            _statModifiers.TryAdd(name, new List<StatModifier>());
            _statModifiers[name].Clear();
        }
    }
}
