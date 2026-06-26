using System.Collections.Generic;
using System.Linq;
using R3;
using Types.Enums;
using UnityEngine;

namespace Services {
    public class UnlockService : IService, System.IDisposable {
        private readonly HashSet<string> _unlockedItems;
        private readonly Dictionary<string, ReactiveProperty<bool>> _unlockStates = new();

        public UnlockService() {
            _unlockedItems = new();
            UnlockItem(nameof(GovernmentInteractionType.MayorOffice));
        }
        
        public void UnlockItem(string upgradeId) {
            if (!_unlockedItems.Add(upgradeId)) {
                Debug.LogWarning($"Item {upgradeId} is already unlocked");
                return;
            }

            if (_unlockStates.TryGetValue(upgradeId, out var state)) {
                state.Value = true;
            }
        }
        
        public bool IsItemUnlocked(string upgradeId) {
            return _unlockedItems.Contains(upgradeId);
        }

        public Observable<bool> ObserveItemUnlocked(string itemId) {
            if (!_unlockStates.TryGetValue(itemId, out var state)) {
                state = new ReactiveProperty<bool>(IsItemUnlocked(itemId));
                _unlockStates.Add(itemId, state);
            }

            return state;
        }

        public void Dispose() {
            foreach (var state in _unlockStates.Values.ToArray()) {
                state.Dispose();
            }

            _unlockStates.Clear();
        }
    }
}
