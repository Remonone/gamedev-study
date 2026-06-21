using System;
using Newtonsoft.Json.Linq;
using Services.Player;
using R3;
using Save;
using Types.Modifiers.Definitions.Values;
using UnityEngine;

namespace Services {
    public class TickService : IService, IDisposable, ISaveable, IStartable {
        private readonly CompositeDisposable _disposable = new();

        private readonly EconomyService _economyService;
        private readonly BuildingWatcherService _buildingWatcherService;
        private readonly Storage _storage;
        private readonly StateBenefitCalculationService _stateCalculationService;
        
        public TickService(EconomyService economyService, BuildingWatcherService buildingWatcherService,
            Storage storage, StateBenefitCalculationService stateCalculationService) {
            _economyService = economyService;
            _buildingWatcherService = buildingWatcherService;
            _storage = storage;
            _stateCalculationService = stateCalculationService;
        }

        private void Tick(double time, bool shouldCalculateCrit = true) {
            foreach (var building in _buildingWatcherService.BuildingsByName.Values) {
                if(building.Level <= 0) continue;
                var cache = _economyService.ComputeStatsForBuilding(building);
                var interval = (double)(1.0f / Math.Max(0.001f, cache.Frequency));
                var elapsed = time - building.LastTimeActivated;
                
                if (elapsed < interval) continue;
                
                var ticks = (int)(elapsed / interval);
                if (ticks <= 0) continue;
                
                building.LastTimeActivated += ticks * interval;
                var income = Value.Zero;
                for (int tick = 0; tick < ticks; tick++) {
                    var benefit = cache.Income;
                    _stateCalculationService.CalculateBenefits(building, ref benefit);
                    if (shouldCalculateCrit) {
                        _stateCalculationService.CalculateCritChance(building, ref benefit);
                    }
                    income += benefit;
                }
                _storage.AddMoney(building.Definition.Type, income);
            }
        }

        public void Dispose() {
            _disposable?.Dispose();
        }
        
        private long CurrentTime => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public string SaveKey => "Ticks";
        public int Priority => -100;

        public JToken Save() {
            return new JObject(new JProperty("LastTick", CurrentTime));
        }

        public void Load(JToken data) {
            long lastSave = data.Value<long>("LastTick");
            long currentTime = CurrentTime;
            Tick(currentTime - lastSave, false);
            foreach (var building in _buildingWatcherService.BuildingsByName.Values) {
                building.LastTimeActivated = Time.timeAsDouble;
            }
        }

        public void StartService() {
            Observable.EveryUpdate()
                .Subscribe(_ => Tick(Time.timeAsDouble))
                .AddTo(_disposable);
        }
    }
}
