using System;
using System.Collections.Generic;
using R3;
using Services.Components;
using Types.Enums;
using Types.QTE;
using Types.Values;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace Services.QTE {
    public sealed class QteService : IService, IStartable, IDisposable {
        private const float MinSpawnIntervalSeconds = 0.1f;

        private readonly CompositeDisposable _disposable = new();
        private readonly QteConfig _config;
        private readonly QteRewardService _rewardService;
        private readonly Random _rng;
        private readonly WorldCastService _worldCastService;
        private readonly QteModifierAggregator _qteModifierAggregator;

        private readonly List<QteSpawnMarker> _markers = new();
        private readonly List<ActiveQte> _activeQtes = new();
        private float _spawnTimerSeconds;
        private DisposableBag _bag;

        public QteService(QteConfig config,
            QteRewardService rewardService,
            QteModifierAggregator modifierAggregator,
            WorldCastService worldCastService) {
            _config = config;
            _rewardService = rewardService;
            _qteModifierAggregator = modifierAggregator;
            _rng = new Random();
            _bag = new DisposableBag();
            _worldCastService = worldCastService;
        }

        public void StartService() {
            CacheMarkers();
            _spawnTimerSeconds = ResolveSpawnIntervalSeconds();

            Observable.EveryUpdate()
                .Subscribe(_ => Tick(Time.deltaTime))
                .AddTo(_disposable);
        }

        private void CacheMarkers() {
            _markers.Clear();
            var markers = Object.FindObjectsByType<QteSpawnMarker>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
            if (markers == null) return;

            for (var i = 0; i < markers.Length; i++) {
                if (markers[i] != null) {
                    _markers.Add(markers[i]);
                }
            }
        }

        private void Tick(float deltaTime) { 
            TickActiveQtes(deltaTime);

            _spawnTimerSeconds -= deltaTime;
            if (_spawnTimerSeconds > 0f) return;

            TrySpawnBatch();
            _spawnTimerSeconds = ResolveSpawnIntervalSeconds();
        }

        private void TickActiveQtes(float deltaTime) {
            for (var i = _activeQtes.Count - 1; i >= 0; i--) {
                var active = _activeQtes[i];
                active.RemainingSeconds -= deltaTime;
                if (active.RemainingSeconds <= 0f) {
                    DespawnAt(i);
                }
            }
        }

        private void TrySpawnBatch() {
            if (!CanSpawn()) return;

            var spawnCount = ResolveSpawnCount();
            var freeCount = CountFreeMarkers();
            if (spawnCount > freeCount) {
                spawnCount = freeCount;
            }

            for (var i = 0; i < spawnCount; i++) {
                var marker = PickFreeMarker();
                if (marker == null) return;

                TrySpawn(marker);
            }
        }

        private bool CanSpawn() {
            return _config != null
                   && _config.Prefab != null
                   && _markers.Count > 0
                   && _config.Rewards != null
                   && _config.Rewards.Count > 0;
        }

        private int ResolveSpawnCount() {
            var multiplier = _config.SpawnMultiplier;
            var guaranteed = Mathf.FloorToInt(multiplier);
            var fractional = multiplier - guaranteed;
            var count = guaranteed;

            if (fractional > 0f && _rng.NextDouble() < fractional) {
                count++;
            }

            return Mathf.Max(0, count);
        }

        private int CountFreeMarkers() {
            var count = 0;
            for (var i = 0; i < _markers.Count; i++) {
                if (!IsMarkerOccupied(_markers[i])) {
                    count++;
                }
            }

            return count;
        }

        private QteSpawnMarker PickFreeMarker() {
            var freeCount = CountFreeMarkers();
            if (freeCount <= 0) return null;

            var selectedFreeIndex = _rng.Next(0, freeCount);
            for (var i = 0; i < _markers.Count; i++) {
                var marker = _markers[i];
                if (IsMarkerOccupied(marker)) continue;

                if (selectedFreeIndex == 0) {
                    return marker;
                }

                selectedFreeIndex--;
            }

            return null;
        }

        private bool IsMarkerOccupied(QteSpawnMarker marker) {
            for (var i = 0; i < _activeQtes.Count; i++) {
                if (_activeQtes[i].Marker == marker) {
                    return true;
                }
            }

            return false;
        }

        private void TrySpawn(QteSpawnMarker marker) {
            if (!TrySelectReward(out var reward, out var resource)) return;

            var view = Object.Instantiate(_config.Prefab, marker.SpawnPosition, Quaternion.identity);
            if (view == null) return;

            var active = new ActiveQte {
                Marker = marker,
                View = view,
                RemainingSeconds = _config.LifetimeSeconds,
                ClicksRemaining = RollClicksRequired(),
                Reward = reward,
                Resource = resource
            };

            active.ClickSubscription = _worldCastService.QTEClicked
                .Where(qte => qte.Equals(active.View))
                .Subscribe(_ => HandleClicked(active)).AddTo(ref _bag);
            _activeQtes.Add(active);
        }

        private bool TrySelectReward(out QteRewardDefinition reward, out GovernmentInteractionType resource) {
            reward = null;
            resource = default;

            var rewards = _config.Rewards;
            if (rewards == null || rewards.Count == 0) return false;

            for (var attempt = 0; attempt < rewards.Count; attempt++) {
                var selected = rewards[_rng.Next(0, rewards.Count)];
                if (selected == null || !selected.TrySelectResource(_rng, out resource)) {
                    continue;
                }

                reward = selected;
                return true;
            }

            return false;
        }

        private int RollClicksRequired() {
            return _config.BaseClicksRequired + _rng.Next(0, _config.ClicksRandomStep + 1);
        }

        private void HandleClicked(ActiveQte active) {
            var index = _activeQtes.IndexOf(active);
            if (index < 0) return;

            _rewardService.GrantReward(active.Resource, active.Reward);
            active.ClicksRemaining--;

            if (active.ClicksRemaining <= 0) {
                DespawnAt(index);
            }
        }

        private float ResolveSpawnIntervalSeconds() {
            var randomOffset = NextFloat(-_config.SpawnIntervalRandomStepSeconds, _config.SpawnIntervalRandomStepSeconds);
            var interval = _config.BaseSpawnIntervalSeconds + randomOffset;
            interval = _qteModifierAggregator.GetModifierBasedValue(interval, QteModifierType.SpawnIntervalSeconds);
            return Mathf.Max(MinSpawnIntervalSeconds, interval);
        }

        private float NextFloat(float minInclusive, float maxInclusive) {
            return minInclusive + (float)_rng.NextDouble() * (maxInclusive - minInclusive);
        }

        private void DespawnAt(int index) {
            if (index < 0 || index >= _activeQtes.Count) return;

            var active = _activeQtes[index];
            active.Dispose();
            _activeQtes.RemoveAt(index);
        }

        public void Dispose() {
            _disposable.Dispose();

            for (var i = _activeQtes.Count - 1; i >= 0; i--) {
                DespawnAt(i);
            }
        }

        private sealed class ActiveQte : IDisposable {
            public QteSpawnMarker Marker;
            public QteObject View;
            public float RemainingSeconds;
            public int ClicksRemaining;
            public QteRewardDefinition Reward;
            public GovernmentInteractionType Resource;
            public IDisposable ClickSubscription;

            public void Dispose() {
                ClickSubscription?.Dispose();
                ClickSubscription = null;

                if (View == null) return;

                Object.Destroy(View.gameObject);
                View = null;
            }
        }
    }
}
