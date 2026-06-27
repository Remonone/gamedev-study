using System;
using System.Collections.Generic;
using R3;
using Services.Components;
using Services.Player;
using Types.Enums;
using Types.QTE;
using Types.Upgrades.Effects;
using Types.Values;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace Services.QTE {
    public sealed class QteService : IService, IStartable, IDisposable {
        private const float MinSpawnIntervalSeconds = 0.1f;

        private readonly CompositeDisposable _disposable = new();
        private readonly QteConfig _config;
        private readonly Storage _storage;
        private readonly PracticeService _practiceService;
        private readonly UpgradeService _upgradeService;
        private readonly Random _rng;
        private readonly WorldCastService _worldCastService;

        private readonly List<QteSpawnMarker> _markers = new();
        private readonly List<ActiveQte> _activeQtes = new();
        private float _spawnTimerSeconds;
        private DisposableBag _bag;

        public QteService(QteConfig config, Storage storage, PracticeService practiceService, UpgradeService upgradeService, WorldCastService worldCastService) {
            _config = config;
            _storage = storage;
            _practiceService = practiceService;
            _upgradeService = upgradeService;
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

            GrantReward(active);
            active.ClicksRemaining--;

            if (active.ClicksRemaining <= 0) {
                DespawnAt(index);
            }
        }

        private void GrantReward(ActiveQte active) {
            var currentAmount = _storage.GetByType(active.Resource);
            var rewardAmount = currentAmount * active.Reward.CurrentAmountMultiplier;
            if (rewardAmount <= Value.Zero) return;
            Debug.Log($"Granting {rewardAmount} {active.Resource} to {active.Marker.gameObject.name}");
            _storage.AddMoney(active.Resource, rewardAmount);
        }

        private float ResolveSpawnIntervalSeconds() {
            var randomOffset = NextFloat(-_config.SpawnIntervalRandomStepSeconds, _config.SpawnIntervalRandomStepSeconds);
            var interval = _config.BaseSpawnIntervalSeconds + randomOffset;
            interval = ApplyModifiers(interval, QteModifierType.SpawnIntervalSeconds);
            return Mathf.Max(MinSpawnIntervalSeconds, interval);
        }

        private float ApplyModifiers(float baseValue, QteModifierType type) {
            var flat = 0f;
            var percent = 0f;
            var multiplier = 1f;
            var hasOverride = false;
            var overridePriority = int.MinValue;
            var overrideValue = baseValue;

            ApplyPracticeModifiers(type, ref flat, ref percent, ref multiplier, ref hasOverride, ref overridePriority, ref overrideValue);
            ApplyUpgradeModifiers(type, ref flat, ref percent, ref multiplier, ref hasOverride, ref overridePriority, ref overrideValue);

            if (hasOverride) return overrideValue;
            return ((baseValue + flat) * (1f + percent)) * multiplier;
        }

        private void ApplyPracticeModifiers(QteModifierType type, ref float flat, ref float percent, ref float multiplier,
            ref bool hasOverride, ref int overridePriority, ref float overrideValue) {
            var practices = _practiceService?.OwnedPracticeDefinitions;
            if (practices == null) return;

            for (var i = 0; i < practices.Count; i++) {
                var effects = practices[i]?.QteEffects;
                ApplyModifierList(effects, type, ref flat, ref percent, ref multiplier, ref hasOverride, ref overridePriority,
                    ref overrideValue);
            }
        }

        private void ApplyUpgradeModifiers(QteModifierType type, ref float flat, ref float percent, ref float multiplier,
            ref bool hasOverride, ref int overridePriority, ref float overrideValue) {
            var upgrades = _upgradeService?.GetAllUpgradeStates();
            if (upgrades == null) return;

            for (var i = 0; i < upgrades.Count; i++) {
                var upgrade = upgrades[i];
                if (upgrade == null || upgrade.Level <= 0 || upgrade.Definition?.Effects == null) continue;

                var effects = upgrade.Definition.Effects;
                for (var effectIndex = 0; effectIndex < effects.Length; effectIndex++) {
                    if (effects[effectIndex] is not QteUpgradeEffect qteEffect) continue;
                    ApplyModifierList(qteEffect.Effects, type, ref flat, ref percent, ref multiplier, ref hasOverride,
                        ref overridePriority, ref overrideValue);
                }
            }
        }

        private static void ApplyModifierList(IReadOnlyList<QteModifierEffect> effects, QteModifierType type, ref float flat,
            ref float percent, ref float multiplier, ref bool hasOverride, ref int overridePriority, ref float overrideValue) {
            if (effects == null) return;

            for (var i = 0; i < effects.Count; i++) {
                var effect = effects[i];
                if (effect == null || effect.Type != type) continue;

                switch (effect.Operation) {
                    case ModifierOp.AddFlat:
                        flat += effect.Value;
                        break;
                    case ModifierOp.AddPercent:
                        percent += effect.Value;
                        break;
                    case ModifierOp.Multiply:
                        multiplier *= effect.Value;
                        break;
                    case ModifierOp.Override:
                        if (!hasOverride || effect.Priority > overridePriority) {
                            hasOverride = true;
                            overridePriority = effect.Priority;
                            overrideValue = effect.Value;
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
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
