using System;
using System.Collections.Generic;
using R3;
using Types.Buildings;
using Types.QTE;
using UnityEngine;

namespace Services.QTE {
    public sealed class QteWorkerService : IService, IStartable, IDisposable {
        private const int MaxCatchUpClicksPerWorkerFrame = 4;

        private readonly QteService _qteService;
        private readonly QteModifierAggregator _modifierAggregator;
        private readonly BuildingUpgradeService _buildingUpgradeService;
        private readonly BuildingWatcherService _buildingWatcherService;
        private readonly UnlockService _unlockService;
        private readonly CompositeDisposable _disposable = new();
        private readonly List<float> _workerTimers = new();
        private readonly System.Random _rng = new();

        public QteWorkerService(
            QteService qteService,
            QteModifierAggregator modifierAggregator,
            BuildingUpgradeService buildingUpgradeService,
            BuildingWatcherService buildingWatcherService,
            UnlockService unlockService) {
            _qteService = qteService;
            _modifierAggregator = modifierAggregator;
            _buildingUpgradeService = buildingUpgradeService;
            _buildingWatcherService = buildingWatcherService;
            _unlockService = unlockService;
        }

        public void StartService() {
            Observable.EveryUpdate()
                .Subscribe(_ => Tick(Time.deltaTime))
                .AddTo(_disposable);
        }

        private void Tick(float deltaTime) {
            var snapshot = _modifierAggregator.ResolveWorkerSnapshot();
            ResizeTimers(snapshot.Count);

            if (snapshot.Count <= 0 || snapshot.ClickFrequency <= 0f) return;

            var interval = 1f / snapshot.ClickFrequency;
            for (var i = 0; i < _workerTimers.Count; i++) {
                _workerTimers[i] += deltaTime;
                var clicksThisFrame = 0;

                while (_workerTimers[i] >= interval && clicksThisFrame < MaxCatchUpClicksPerWorkerFrame) {
                    _workerTimers[i] -= interval;
                    clicksThisFrame++;

                    if (!_qteService.TryWorkerClick(snapshot.IncomeMultiplier, out var completed)) continue;
                    if (completed) TryUpgradeRandomEligibleBuilding(snapshot.BuildingUpgradeChance);
                }

                if (clicksThisFrame >= MaxCatchUpClicksPerWorkerFrame && _workerTimers[i] >= interval) {
                    _workerTimers[i] = 0f;
                }
            }
        }

        private void ResizeTimers(int count) {
            while (_workerTimers.Count < count) _workerTimers.Add(0f);
            if (_workerTimers.Count > count) _workerTimers.RemoveRange(count, _workerTimers.Count - count);
        }

        private void TryUpgradeRandomEligibleBuilding(float chance) {
            if (chance <= 0f || _rng.NextDouble() >= chance) return;

            BuildingState selected = null;
            var eligibleCount = 0;
            foreach (var building in _buildingWatcherService.BuildingsByName.Values) {
                if (!IsEligible(building)) continue;

                eligibleCount++;
                if (_rng.Next(eligibleCount) == 0) {
                    selected = building;
                }
            }

            if (selected != null) {
                _buildingUpgradeService.UpgradeBuilding(selected.Definition.Name, 1);
            }
        }

        private bool IsEligible(BuildingState building) {
            return building?.Definition != null
                   && building.Definition.IsUpgradeable
                   && building.Level > 0
                   && _unlockService.IsItemUnlocked(building.Definition.Type.ToString());
        }

        public void Dispose() {
            _disposable.Dispose();
        }
    }
}
