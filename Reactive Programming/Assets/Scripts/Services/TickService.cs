using System;
using Newtonsoft.Json;
using Player;
using R3;
using Save;
using UnityEngine;

namespace Services {
    public class TickService : IService, IDisposable, ISaveable {
        private readonly CompositeDisposable _disposable = new();

        private readonly EconomyService _economyService;
        private readonly BuildingWatcherService _buildingWatcherService;
        private readonly Storage _storage;
        
        public TickService(EconomyService economyService, BuildingWatcherService buildingWatcherService, Storage storage) {
            _economyService = economyService;
            _buildingWatcherService = buildingWatcherService;
            _storage = storage;
            
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
                _storage.AddMoney(building.Definition.Type, (long)cache.Income * ticks);
            }
        }

        public void Dispose() {
            _disposable?.Dispose();
        }
        
        private long CurrentTime => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public string SaveKey => "Ticks";
        public string Save() {
            return JsonConvert.SerializeObject(CurrentTime);
        }

        public void Load(object data) {
            long lastSave = JsonConvert.DeserializeObject<long>((string)data);
            long currentTime = CurrentTime;
            Tick(currentTime - lastSave);
        }
    }
}