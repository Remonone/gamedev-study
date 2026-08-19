using System;
using System.Collections.Generic;
using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Modifiers;
using Data.Upgrades;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using R3;
using Services.Locator;
using UnityEngine;
using Utils;

namespace Services {
    public sealed class MetaProgressionService : IService, IInitialize, ISaveable {
        public const string SaveSectionId = "MetaProgression";
        public const long UnlockThreshold = 5L;

        private readonly Subject<Unit> _changed = new();
        private readonly CompositeDisposable _subscriptions = new();
        private Dictionary<string, UpgradeNodeState> _states = new(StringComparer.Ordinal);
        private Dictionary<string, int> _unresolvedLevels = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _previewLevels = new(StringComparer.Ordinal);
        private HashSet<UpgradeNodeState> _owned = new();
        private RestoreData _deferredRestore;
        private IAssetListLease<MetaUpgradeNodeDefinition> _lease;
        private IAssetProvider _assetProvider;
        private ICacheInvalidator _cacheInvalidator;
        private WalletService _wallet;
        private UpgradeService _ordinaryUpgrades;
        private Value _moneyPeak;
        private long _bankedPoints;
        private long _previousIterationPoints;
        private long _currentIterationPoints;
        private long _spentPoints;
        private bool _catalogBuilt;
        private bool _catalogAvailable;

        public MetaProgressionService() { }

        internal MetaProgressionService(IAssetProvider assetProvider) {
            _assetProvider = assetProvider ?? throw new ArgumentNullException(nameof(assetProvider));
        }

        public string SaveId => SaveSectionId;
        public IReadOnlyCollection<UpgradeNodeState> Nodes => _states.Values;
        public IReadOnlyCollection<UpgradeNodeState> OwnedMetaUpgrades => _owned;
        public Observable<Unit> Changed => _changed;
        public Value MoneyPeak => _moneyPeak;
        public long BankedPoints => _bankedPoints;
        public long PreviousIterationPoints => _previousIterationPoints;
        public long CurrentIterationPoints => _currentIterationPoints;
        public long AvailablePoints => SaturatingAdd(_bankedPoints, _currentIterationPoints);
        public long SpentPoints => _spentPoints;
        public long PreviewAvailablePoints => AvailablePoints >= _spentPoints ? AvailablePoints - _spentPoints : 0L;
        public bool HasPreviewPurchases => _previewLevels.Count > 0;
        public bool IsEligible => MetaPointCalculator.IsEligible(
            _currentIterationPoints, _previousIterationPoints, UnlockThreshold);
        public bool IsCatalogAvailable => _catalogAvailable;

        public async UniTask InitializeAsync(IServiceScope scope) {
            _cacheInvalidator = scope.Get<ICacheInvalidator>();
            _wallet = scope.Get<WalletService>();
            _ordinaryUpgrades = scope.Get<UpgradeService>();
            _assetProvider ??= scope.Container.Get<IAssetProvider>();

            try {
                _lease = await _assetProvider.LoadAssetsByLabelAsync<MetaUpgradeNodeDefinition>(
                    AddressableConstants.META_UPGRADE_LABEL);
            }
            catch (Exception exception) {
                Debug.LogWarning(
                    $"Meta upgrade catalog '{AddressableConstants.META_UPGRADE_LABEL}' is unavailable. " +
                    $"Saved meta ownership will be preserved, but purchases are disabled.\n{exception}");
                BuildDefinitions(Array.Empty<MetaUpgradeNodeDefinition>());
            }
            if (_lease != null) BuildDefinitions(_lease.Assets);

            if (_deferredRestore != null) {
                ApplyRestore(_deferredRestore, false);
                _deferredRestore = null;
            }

            ReconcileMoneyPeak(_wallet.CurrentBalance);
            _wallet.BalanceChanged.Subscribe(balance => ReconcileMoneyPeak(ToValue(balance))).AddTo(_subscriptions);
            _ordinaryUpgrades.Changed.Subscribe(_ => RefreshCurrentPoints()).AddTo(_subscriptions);
            RefreshCurrentPoints(true);
        }

        internal void BuildDefinitions(IReadOnlyList<MetaUpgradeNodeDefinition> assets) {
            if (assets == null) throw new ArgumentNullException(nameof(assets));

            var states = new Dictionary<string, UpgradeNodeState>(StringComparer.Ordinal);
            for (int index = 0; index < assets.Count; index++) {
                MetaUpgradeNodeDefinition definition = assets[index];
                ValidateDefinition(definition);
                if (definition.GetType() != typeof(MetaUpgradeNodeDefinition)) {
                    throw new InvalidOperationException("Meta upgrade catalogs accept only exact MetaUpgradeNodeDefinition assets.");
                }
                if (!states.TryAdd(definition.Id, new UpgradeNodeState(definition))) {
                    throw new InvalidOperationException($"Duplicate meta upgrade ID '{definition.Id}'.");
                }
            }

            _states = states;
            _owned = new HashSet<UpgradeNodeState>();
            ClearPreview();
            _catalogAvailable = states.Count > 0;
            _catalogBuilt = true;
            if (!_catalogAvailable) {
                Debug.LogWarning("The meta upgrade catalog is empty. Meta purchases are disabled until a node is authored.");
            }
        }

        public UpgradeNodeState GetUpgrade(string id) {
            return string.IsNullOrWhiteSpace(id) ? null : _states.GetValueOrDefault(id);
        }

        public int EffectiveLevel(string id) {
            if (string.IsNullOrWhiteSpace(id)) return 0;
            return _previewLevels.TryGetValue(id, out int previewLevel)
                ? previewLevel
                : GetUpgrade(id)?.Level ?? 0;
        }

        public bool IsPreviewed(string id) {
            return !string.IsNullOrWhiteSpace(id) && _previewLevels.ContainsKey(id);
        }

        public bool TryResolveCost(UpgradeNodeState state, out long cost) {
            cost = 0L;
            if (state?.Definition?.CostFormula == null) return false;
            int level = EffectiveLevel(state.Definition.Id);
            if (state.Definition.IsTerminalLevel(level)) return false;
            Value value = state.Definition.CostFormula.Evaluate(level);
            if (value.IsZero) return false;
            double numeric = value.ToDouble();
            if (double.IsNaN(numeric) || double.IsInfinity(numeric) || numeric <= 0d ||
                numeric > long.MaxValue || Math.Floor(numeric) != numeric) return false;
            cost = (long)numeric;
            return cost > 0L;
        }

        public bool CanPurchase(string id) {
            UpgradeNodeState state = GetUpgrade(id);
            return _catalogAvailable && IsEligible && TryResolveCost(state, out long cost) && cost <= PreviewAvailablePoints;
        }

        public bool TryStagePurchase(string id, out long cost) {
            cost = 0L;
            UpgradeNodeState upgrade = GetUpgrade(id);
            if (!_catalogAvailable || !IsEligible || !TryResolveCost(upgrade, out cost) || cost > PreviewAvailablePoints) {
                return false;
            }

            _previewLevels[id] = EffectiveLevel(id) + 1;
            _spentPoints = SaturatingAdd(_spentPoints, cost);
            _changed.OnNext(Unit.Default);
            return true;
        }

        internal bool TryCreatePurchasedState(string id, out JToken state, out long cost) {
            state = null;
            cost = 0L;
            UpgradeNodeState upgrade = GetUpgrade(id);
            if (!_catalogAvailable || !IsEligible || !TryResolveCost(upgrade, out cost) || cost > AvailablePoints) {
                return false;
            }

            var levels = new Dictionary<string, int>(_unresolvedLevels, StringComparer.Ordinal);
            foreach (KeyValuePair<string, UpgradeNodeState> pair in _states) {
                if (pair.Value.Level > 0) levels[pair.Key] = pair.Value.Level;
            }
            levels[id] = EffectiveLevel(id) + 1;
            state = SerializeLevels(levels, AvailablePoints - cost, _currentIterationPoints, Value.Zero);
            return true;
        }

        public bool TryCreateConfirmedPreviewState(out JToken state) {
            state = null;
            if (!HasPreviewPurchases || _spentPoints > AvailablePoints) return false;

            var levels = new Dictionary<string, int>(_unresolvedLevels, StringComparer.Ordinal);
            foreach (KeyValuePair<string, UpgradeNodeState> pair in _states) {
                int level = EffectiveLevel(pair.Key);
                if (level > 0) levels[pair.Key] = level;
            }

            state = SerializeLevels(levels, PreviewAvailablePoints, _currentIterationPoints, Value.Zero);
            return true;
        }

        public JToken Serialize() {
            var levels = new Dictionary<string, int>(_unresolvedLevels, StringComparer.Ordinal);
            foreach (KeyValuePair<string, UpgradeNodeState> pair in _states) {
                if (pair.Value.Level > 0) levels[pair.Key] = pair.Value.Level;
            }
            return SerializeLevels(levels, _bankedPoints, _previousIterationPoints, _moneyPeak);
        }

        public void Deserialize(JToken state) {
            RestoreData restored = ParseRestore(state);
            ClearPreview();
            if (!_catalogBuilt) {
                _deferredRestore = restored;
                return;
            }

            ApplyRestore(restored, true);
        }

        public void Dispose() {
            _subscriptions.Dispose();
            _changed.Dispose();
            _lease?.Dispose();
            _lease = null;
            _states.Clear();
            _owned.Clear();
            _unresolvedLevels.Clear();
            ClearPreview();
            _deferredRestore = null;
        }

        private void ReconcileMoneyPeak(Value balance) {
            if (balance <= _moneyPeak) return;
            _moneyPeak = balance;
            RefreshCurrentPoints();
        }

        private void RefreshCurrentPoints(bool forceNotify = false) {
            long markedLevels = MetaPointCalculator.CountMarkedLevels(_ordinaryUpgrades?.OwnedUpgrades);

            long updated = MetaPointCalculator.Calculate(markedLevels, _moneyPeak);
            if (!forceNotify && updated == _currentIterationPoints) return;
            _currentIterationPoints = updated;
            _changed.OnNext(Unit.Default);
        }

        private void ApplyRestore(RestoreData restored, bool notify) {
            ClearPreview();
            var states = new Dictionary<string, UpgradeNodeState>(_states.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, UpgradeNodeState> pair in _states) {
                states.Add(pair.Key, new UpgradeNodeState(pair.Value.Definition));
            }

            var owned = new HashSet<UpgradeNodeState>();
            var unresolved = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> pair in restored.Levels) {
                if (!states.TryGetValue(pair.Key, out UpgradeNodeState upgrade)) {
                    unresolved.Add(pair.Key, pair.Value);
                    Debug.LogWarning($"Saved meta upgrade '{pair.Key}' is unavailable and was preserved as unresolved.");
                    continue;
                }

                int level = pair.Value;
                if (upgrade.Definition.HasLevelCap && level > upgrade.Definition.MaxLevel) {
                    Debug.LogWarning($"Saved level {level} for meta upgrade '{pair.Key}' was clamped.");
                    level = upgrade.Definition.MaxLevel;
                }
                upgrade.Level = level;
                upgrade.Effectiveness = 1f;
                upgrade.CurrentState = upgrade.Definition.IsTerminalLevel(level)
                    ? UpgradeNodeState.State.Completed
                    : UpgradeNodeState.State.InProgress;
                owned.Add(upgrade);
            }

            _states = states;
            _owned = owned;
            _unresolvedLevels = unresolved;
            _bankedPoints = restored.BankedPoints;
            _previousIterationPoints = restored.PreviousIterationPoints;
            _moneyPeak = restored.MoneyPeak;
            _cacheInvalidator?.InvalidateAll();
            RefreshCurrentPoints(notify);
        }

        private static JToken SerializeLevels(IReadOnlyDictionary<string, int> levels, long banked,
            long previous, Value peak) {
            var upgrades = new JArray();
            foreach (KeyValuePair<string, int> pair in levels) {
                upgrades.Add(new JObject { ["id"] = pair.Key, ["level"] = pair.Value });
            }

            return new JObject {
                ["bankedPoints"] = banked,
                ["previousIterationPoints"] = previous,
                ["moneyPeakStored"] = peak.Stored,
                ["moneyPeakDegree"] = peak.Base.Degree,
                ["upgrades"] = upgrades
            };
        }

        private void ClearPreview() {
            _previewLevels.Clear();
            _spentPoints = 0L;
        }

        private static RestoreData ParseRestore(JToken state) {
            if (state is not JObject root || root["bankedPoints"]?.Type != JTokenType.Integer ||
                root["previousIterationPoints"]?.Type != JTokenType.Integer ||
                root["upgrades"] is not JArray upgrades ||
                !TryReadValue(root["moneyPeakStored"], root["moneyPeakDegree"], out Value peak)) {
                throw new JsonSerializationException("Meta progression save data is incomplete.");
            }

            long banked = root["bankedPoints"].Value<long>();
            long previous = root["previousIterationPoints"].Value<long>();
            if (banked < 0L || previous < 0L) {
                throw new JsonSerializationException("Meta progression point values cannot be negative.");
            }

            var levels = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (JToken token in upgrades) {
                if (token is not JObject item || item["id"]?.Type != JTokenType.String ||
                    item["level"]?.Type != JTokenType.Integer) {
                    throw new JsonSerializationException("Each saved meta upgrade requires an ID and level.");
                }
                string id = item["id"].Value<string>();
                int level = item["level"].Value<int>();
                if (string.IsNullOrWhiteSpace(id) || level <= 0 || !levels.TryAdd(id, level)) {
                    throw new JsonSerializationException("Saved meta upgrade IDs and levels must be unique and valid.");
                }
            }

            return new RestoreData(banked, previous, peak, levels);
        }

        private static bool TryReadValue(JToken storedToken, JToken degreeToken, out Value value) {
            value = default;
            if (storedToken?.Type is not (JTokenType.Integer or JTokenType.Float) ||
                degreeToken?.Type != JTokenType.Integer) return false;
            double stored = storedToken.Value<double>();
            int degree = degreeToken.Value<int>();
            bool invalid = double.IsNaN(stored) || double.IsInfinity(stored) || stored < 0d || stored >= 1000d ||
                           degree < 0 || stored == 0d && degree != 0 || degree > 0 && stored < 1d;
            if (invalid) return false;
            var candidate = new Value(stored, new BaseValue(degree));
            if (candidate.Stored != stored || candidate.Base.Degree != degree) return false;
            value = candidate;
            return true;
        }

        private static Value ToValue(IValue value) {
            return value is Value typed ? typed : new Value(value.Stored, value.Base);
        }

        private static long SaturatingAdd(long left, long right) {
            if (right > 0L && left > long.MaxValue - right) return long.MaxValue;
            return left + right;
        }

        private static void ValidateDefinition(MetaUpgradeNodeDefinition definition) {
            if (definition == null) throw new InvalidOperationException("Meta upgrade catalog contains a null definition.");
            if (string.IsNullOrWhiteSpace(definition.Id)) {
                throw new InvalidOperationException($"Meta upgrade definition '{definition.name}' has an empty ID.");
            }
            if (definition.MaxLevel < 0) throw new InvalidOperationException($"Meta upgrade '{definition.Id}' has a negative level cap.");
            if (definition.CostFormula == null) throw new InvalidOperationException($"Meta upgrade '{definition.Id}' has no cost formula.");
            if (definition.FeatureUnlockIds is { Length: > 0 } || definition.StatisticRequirements is { Length: > 0 } ||
                definition.GrantsMetaCurrencyPoint) {
                Debug.LogWarning($"Meta upgrade '{definition.Id}' has ordinary-only fields; they will be ignored.");
            }

            var modifierIds = new HashSet<string>(StringComparer.Ordinal);
            ModifierDefinition[] modifiers = definition.Modifiers;
            if (modifiers == null) return;
            for (int definitionIndex = 0; definitionIndex < modifiers.Length; definitionIndex++) {
                ModifierDefinition modifierDefinition = modifiers[definitionIndex];
                if (modifierDefinition?.NumericModifiers == null) continue;
                for (int modifierIndex = 0; modifierIndex < modifierDefinition.NumericModifiers.Count; modifierIndex++) {
                    NumericModifierDefinition modifier = modifierDefinition.NumericModifiers[modifierIndex];
                    if (modifier == null) continue;
                    if (string.IsNullOrWhiteSpace(modifier.Id) || !modifierIds.Add(modifier.Id)) {
                        throw new InvalidOperationException($"Meta upgrade '{definition.Id}' has missing or duplicate modifier IDs.");
                    }
                }
            }
        }

        private sealed class RestoreData {
            internal readonly long BankedPoints;
            internal readonly long PreviousIterationPoints;
            internal readonly Value MoneyPeak;
            internal readonly Dictionary<string, int> Levels;

            internal RestoreData(long bankedPoints, long previousIterationPoints, Value moneyPeak,
                Dictionary<string, int> levels) {
                BankedPoints = bankedPoints;
                PreviousIterationPoints = previousIterationPoints;
                MoneyPeak = moneyPeak;
                Levels = levels;
            }
        }
    }
}
