using System;
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
    public class EconomyService : IService, IDisposable {
        private readonly ISessionContext _sessionContext;
        private readonly StatResolver _statResolver;
        private readonly BuildingUpgradeService _buildingUpgradeService;
        private readonly BuildingWatcherService _buildingWatcherService;
        private readonly Storage _storage;
        private readonly ProviderRegistryService _providerRegistryService;
        private readonly CompositeDisposable _disposable = new();
        private readonly Dictionary<PurchasePriceCacheKey, Price> _purchasePriceCache = new();
        
        private Subject<BuildingUpdate> _buildingUpdate = new();
        private Subject<Unit> _purchasePricesInvalidated = new();
        
        public Observable<BuildingUpdate> BuildingUpdate => _buildingUpdate;
        public Observable<Unit> PurchasePricesInvalidated => _purchasePricesInvalidated;
        
        public EconomyService(
            ISessionContext sessionContext,
            Storage storage,
            BuildingWatcherService watcherService,
            BuildingUpgradeService buildingUpgradeService,
            ProviderRegistryService providerRegistryService,
            InvalidationService invalidationService) {
            _sessionContext = sessionContext;
            _buildingWatcherService = watcherService;
            _buildingUpgradeService = buildingUpgradeService;
            _providerRegistryService = providerRegistryService;
            _storage = storage;
            _statResolver = new StatResolver();

            invalidationService.Invalidated
                .Subscribe(_ => InvalidatePurchasePriceCache())
                .AddTo(_disposable);
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
            return GetBuildingPurchasePrice(building, levels);
        }

        public bool CanPurchaseBuilding(string name, int levels = 1) {
            var building = _buildingWatcherService.GetBuildingState(name);
            if (building == null || levels <= 0) return false;
            return _storage.CanAfford(GetBuildingPurchasePrice(building, levels));
        }

        public bool PurchaseBuilding(string name, int levels = 1) {
            var building = _buildingWatcherService.GetBuildingState(name);
            if (building == null || levels <= 0) return false;

            var price = GetBuildingPurchasePrice(building, levels);
            if (!_storage.CanAfford(price)) return false;

            _storage.Spend(price);
            _buildingUpgradeService.UpgradeBuilding(name, levels);
            return true;
        }

        private Price GetBuildingPurchasePrice(BuildingState building, int levels) {
            var key = new PurchasePriceCacheKey(building.Definition.Name, building.Level, levels);
            if (_purchasePriceCache.TryGetValue(key, out var cachedPrice)) {
                return cachedPrice;
            }

            var price = CalculatePurchasePrice(building, levels);
            _purchasePriceCache[key] = price;
            return price;
        }

        private void InvalidatePurchasePriceCache() {
            _purchasePriceCache.Clear();
            _purchasePricesInvalidated.OnNext(Unit.Default);
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

        public void Dispose() {
            _disposable.Dispose();
            _buildingUpdate.Dispose();
            _purchasePricesInvalidated.Dispose();
        }

        private readonly struct PurchasePriceCacheKey : IEquatable<PurchasePriceCacheKey> {
            private readonly string _buildingName;
            private readonly int _buildingLevel;
            private readonly int _purchaseLevels;

            public PurchasePriceCacheKey(string buildingName, int buildingLevel, int purchaseLevels) {
                _buildingName = buildingName;
                _buildingLevel = buildingLevel;
                _purchaseLevels = purchaseLevels;
            }

            public bool Equals(PurchasePriceCacheKey other) {
                return _buildingName == other._buildingName &&
                       _buildingLevel == other._buildingLevel &&
                       _purchaseLevels == other._purchaseLevels;
            }

            public override bool Equals(object obj) {
                return obj is PurchasePriceCacheKey other && Equals(other);
            }

            public override int GetHashCode() {
                unchecked {
                    var hashCode = _buildingName != null ? _buildingName.GetHashCode() : 0;
                    hashCode = (hashCode * 397) ^ _buildingLevel;
                    hashCode = (hashCode * 397) ^ _purchaseLevels;
                    return hashCode;
                }
            }
        }
    }
}
