using System;
using System.Collections.Generic;
using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Modifiers;
using Data.Upgrades;
using Newtonsoft.Json.Linq;
using R3;
using Services.Locator;
using Utils;

namespace Services {
    public class UpgradeService : IService, IInitialize, ISaveable {

        private Dictionary<string, UpgradeNodeState> _states;
        private Dictionary<string, int> _upgradeRestore;
        private ICacheInvalidator _cacheInvalidator;
        
        private HashSet<UpgradeNodeState> _ownedUpgrades = new();
        private readonly Subject<Unit> _changed = new();
        
        private WalletService _wallet;
        private IAssetListLease<UpgradeNodeDefinition> _lease;
        private IAssetProvider _assetProvider;
        
        public IReadOnlyCollection<UpgradeNodeState> OwnedUpgrades => _ownedUpgrades;

        public async UniTask InitializeAsync(IServiceScope scope) {
            _cacheInvalidator = scope.Get<ICacheInvalidator>();
            _wallet = scope.Get<WalletService>();
            _assetProvider = scope.Container.Get<IAssetProvider>();
            _lease = await _assetProvider.LoadAssetsByLabelAsync<UpgradeNodeDefinition>(AddressableConstants.UPGRADE_LABEL);
            BuildDefinitions(_lease.Assets);
            RestoreOnPresent();
        }

        private void RestoreOnPresent() {
            if (_upgradeRestore == null) return;
            foreach (var upgrade in _upgradeRestore) {
                if (_states.TryGetValue(upgrade.Key, out var state)) {
                    state.Level = upgrade.Value;
                }
            }
        }

        private void BuildDefinitions(IReadOnlyList<UpgradeNodeDefinition> assets) {
            _states = new Dictionary<string, UpgradeNodeState>();
            foreach (var asset in assets) {
                _states[asset.Id] = new UpgradeNodeState(asset);
            }
        }


        public bool TryUpgrade(string upgradeId) {
            var upgrade = GetUpgrade(upgradeId);
            if (upgrade == null || upgrade.CurrentState is not (UpgradeNodeState.State.Available or UpgradeNodeState.State.InProgress)) {
                return false;
            }

            var price = ResolvePrice(upgrade);
            if (!_wallet.TryWithdrawWallet(price)) {
                return false;
            }
            var updated = new UpgradeNodeState(upgrade);

            updated.Level++;
            updated.CurrentState = updated.Level >= updated.Definition.MaxLevel
                ? UpgradeNodeState.State.Completed
                : UpgradeNodeState.State.InProgress;
            
            _ownedUpgrades.Remove(upgrade);
            _ownedUpgrades.Add(updated);
            
            InvalidateGroups(updated.Definition.Modifiers);

            NotifyChanged();
            return true;
        }

        private void NotifyChanged() {
            _changed.OnNext(Unit.Default);
        }


        private void InvalidateGroups(ModifierDefinition[] definitionModifiers) {
            var affectedGroups = new HashSet<Type>();
            foreach (var modifier in definitionModifiers) {
                affectedGroups.UnionWith(modifier.GetAffectedTypes());
            }

            foreach (var group in affectedGroups) {
                _cacheInvalidator.Invalidate(group);
            }
        }

        private Value ResolvePrice(UpgradeNodeState state) {
            return state.Definition.CostFormula.Evaluate(state.Level);
        }

        private UpgradeNodeState GetUpgrade(string upgradeId) {
            return _states.GetValueOrDefault(upgradeId);
        }

        public string SaveId => "Upgrades";
        public JToken Serialize() {
            var result = new JObject();
            var list = new JArray();
            result.Add("upgrades", list);
            foreach (var upgrade in _ownedUpgrades) {
                list.Add(new JObject {
                    ["id"] = upgrade.Definition.Id,
                    ["level"] = upgrade.Level
                });
            }

            return result;
        }

        public void Deserialize(JToken state) {
            if (state["upgrades"] == null || !(state["upgrades"] is JArray)) return;
            _upgradeRestore = new Dictionary<string, int>();
            foreach (var upgrade in state["upgrades"]) {
                if (upgrade is not JObject data || !data.TryGetValue("id", out var id) ||
                    !data.TryGetValue("level", out var level)) continue;
                _upgradeRestore.Add(id.Value<string>(), level.Value<int>());
            }
        }
        
        public void Dispose() {
            _changed.Dispose();
            _states.Clear();
            _ownedUpgrades.Clear();
            _lease?.Dispose();
            _lease = null;
        }
    }
}