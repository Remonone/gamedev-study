using System.Collections.Generic;
using Types.Enums;
using UnityEngine;

namespace Services {
    public class UnlockService : IService {
        private readonly HashSet<string> _unlockedItems;

        public UnlockService() {
            _unlockedItems = new();
            UnlockItem(nameof(GovernmentInteractionType.MayorOffice));
        }
        
        public void UnlockItem(string upgradeId) {
            if (!_unlockedItems.Add(upgradeId)) {
                Debug.LogWarning($"Item {upgradeId} is already unlocked");
            }
        }
        
        public bool IsItemUnlocked(string upgradeId) {
            return _unlockedItems.Contains(upgradeId);
        }
    }
}