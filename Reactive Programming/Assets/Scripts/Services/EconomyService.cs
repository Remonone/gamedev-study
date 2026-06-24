using System.Collections.Generic;
using System.Linq;
using Types.Buildings;
using Economy;
using Services.Player;
using R3;
using Types.Enums;
using Types.Modifiers;
using Types.Modifiers.Cost;
using Types.Values;

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

        public Price GetBuildingPurchasePrice(string name, int levels = 1) {
            var building = _buildingWatcherService.GetBuildingState(name);
            if (building == null || levels <= 0) return new Price();
            return CalculatePurchasePrice(building, levels);
        }

        public bool CanPurchaseBuilding(string name, int levels = 1) {
            var building = _buildingWatcherService.GetBuildingState(name);
            return ValidateCost(building, levels);
        }

        public void PurchaseBuilding(string name, int levels = 1) {
            var building = _buildingWatcherService.GetBuildingState(name);
            if (!ValidateCost(building, levels)) return;

            var price = CalculatePurchasePrice(building, levels);
            _storage.Spend(price);
            _buildingUpgradeService.UpgradeBuilding(name, levels);
        }

        private bool ValidateCost(BuildingState building, int levels) {
            if (building == null) return false;
            if (levels <= 0) return false;
            return _storage.CanAfford(CalculatePurchasePrice(building, levels));
        }

        private Price CalculatePurchasePrice(BuildingState building, int levels) {
            var total = new Dictionary<GovernmentInteractionType, Value>();

            for (var i = 0; i < levels; i++) {
                AddPrice(total, CalculatePriceForLevel(building, building.Level + i));
            }

            return new Price(total
                .Select(entry => new Price.Entry(entry.Key, entry.Value))
                .ToArray());
        }

        private Price CalculatePriceForLevel(BuildingState building, int level) {
            var projectedBuilding = new BuildingState(building.Definition, level);
            var modifiers = new List<StatModifier>();

            _providerRegistryService.FetchModifiers(_sessionContext, projectedBuilding, modifiers);
            return _statResolver.Resolve(projectedBuilding, modifiers).Cost;
        }

        private static void AddPrice(Dictionary<GovernmentInteractionType, Value> total, Price price) {
            if (price.Entries == null) return;

            foreach (var entry in price.Entries) {
                if (!total.ContainsKey(entry.GovernmentInteractionType)) {
                    total[entry.GovernmentInteractionType] = Value.Zero;
                }

                total[entry.GovernmentInteractionType] += entry.Price;
            }
        }
    }
}
