using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Cysharp.Threading.Tasks;
using Data.Upgrades;
using R3;
using Services.Locator;
using UnityEngine;

namespace Services {
    public sealed class UnlockService : IService, IInitialize {
        private readonly Dictionary<string, string[]> _validGrants = new(StringComparer.Ordinal);
        private readonly HashSet<string> _unlocked = new(StringComparer.Ordinal);
        private readonly Subject<Unit> _changed = new();
        private readonly CompositeDisposable _subscriptions = new();

        private UpgradeService _upgrades;
        private ReadOnlyCollection<string> _unlockedSnapshot = Array.AsReadOnly(Array.Empty<string>());

        public IReadOnlyCollection<string> UnlockedFeatures => _unlockedSnapshot;
        public Observable<Unit> Changed => _changed;

        public UniTask InitializeAsync(IServiceScope scope) {
            _upgrades = scope.Get<UpgradeService>();
            BuildGrantIndex();
            RebuildUnlockedFeatures();
            _upgrades.Changed.Subscribe(_ => RebuildUnlockedFeatures()).AddTo(_subscriptions);
            return UniTask.CompletedTask;
        }

        public bool IsUnlocked(string featureId) {
            return !string.IsNullOrWhiteSpace(featureId) && _unlocked.Contains(featureId);
        }

        public void Dispose() {
            _subscriptions.Dispose();
            _changed.Dispose();
            _validGrants.Clear();
            _unlocked.Clear();
            _unlockedSnapshot = Array.AsReadOnly(Array.Empty<string>());
            _upgrades = null;
        }

        private void BuildGrantIndex() {
            _validGrants.Clear();
            foreach (UpgradeNodeState state in _upgrades.Nodes) {
                string[] configured = state.Definition.FeatureUnlockIds;
                if (configured == null || configured.Length == 0) {
                    _validGrants.Add(state.Definition.Id, Array.Empty<string>());
                    continue;
                }

                var unique = new HashSet<string>(StringComparer.Ordinal);
                var valid = new List<string>(configured.Length);
                for (int index = 0; index < configured.Length; index++) {
                    string featureId = configured[index];
                    if (string.IsNullOrWhiteSpace(featureId)) {
                        Debug.LogWarning(
                            $"Upgrade '{state.Definition.Id}' contains an empty feature unlock at index {index}; it was ignored.");
                        continue;
                    }

                    if (!unique.Add(featureId)) {
                        Debug.LogWarning(
                            $"Upgrade '{state.Definition.Id}' grants feature '{featureId}' more than once; the duplicate was ignored.");
                        continue;
                    }

                    valid.Add(featureId);
                }

                _validGrants.Add(state.Definition.Id, valid.ToArray());
            }
        }

        private void RebuildUnlockedFeatures() {
            var rebuilt = new HashSet<string>(StringComparer.Ordinal);
            foreach (UpgradeNodeState state in _upgrades.OwnedUpgrades) {
                if (!_validGrants.TryGetValue(state.Definition.Id, out string[] grants)) continue;
                for (int index = 0; index < grants.Length; index++) rebuilt.Add(grants[index]);
            }

            if (_unlocked.SetEquals(rebuilt)) return;

            _unlocked.Clear();
            _unlocked.UnionWith(rebuilt);
            var snapshot = new string[_unlocked.Count];
            _unlocked.CopyTo(snapshot);
            Array.Sort(snapshot, StringComparer.Ordinal);
            _unlockedSnapshot = Array.AsReadOnly(snapshot);
            _changed.OnNext(Unit.Default);
        }
    }
}
