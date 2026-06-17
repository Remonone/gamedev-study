using System;
using Components;
using Newtonsoft.Json.Linq;
using Services.Player;
using R3;
using Save;
using UnityEngine;

namespace Services {
    public class TickService : IService, IDisposable, ISaveable {
        private readonly CompositeDisposable _disposable = new();

        private readonly EconomyService _economyService;
        private readonly BuildingWatcherService _buildingWatcherService;
        private readonly Storage _storage;
        private readonly StateBenefitCalculationService _stateCalculationService;
        
        public TickService(EconomyService economyService, BuildingWatcherService buildingWatcherService, Storage storage) {
            _economyService = economyService;
            _buildingWatcherService = buildingWatcherService;
            _storage = storage;
            _stateCalculationService = ServiceLocator.Instance.GetService<StateBenefitCalculationService>();
            
            Observable.EveryUpdate()
                .Subscribe(_ => Tick(Time.timeAsDouble))
                .AddTo(_disposable);
        }

        private void Tick(double time) {
            foreach (var building in _buildingWatcherService.BuildingsByName.Values) {
                if(building.Level <= 0) continue;
                var cache = _economyService.ComputeStatsForBuilding(building);
                var interval = (double)(1.0f / Math.Max(0.001f, cache.Frequency));
                var elapsed = time - building.LastTimeActivated;
                
                if (elapsed < interval) continue;
                
                var ticks = (int)(elapsed / interval);
                if (ticks <= 0) continue;
                
                building.LastTimeActivated += ticks * interval;
                for (int tick = 0; tick < ticks; tick++) {
                    var income = _stateCalculationService.CalculateBenefits(building, cache.Income);
                    _storage.AddMoney(building.Definition.Type, (long)income);
                }
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
            Tick(currentTime - lastSave);
            foreach (var building in _buildingWatcherService.BuildingsByName.Values) {
                building.LastTimeActivated = Time.timeAsDouble;
            }
        }
    }
}