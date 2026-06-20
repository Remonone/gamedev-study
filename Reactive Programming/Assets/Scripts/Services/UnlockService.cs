using System.Collections.Generic;
using Types.Modifiers.Definitions;
using UnityEngine;

namespace Services {
    public class UnlockService : IService {
        private HashSet<string> UnlockedItems;

        public UnlockService() {
            UnlockedItems = new();
            UnlockItem(nameof(GovernmentInteractionType.MayorOffice));
        }
        
        public void UnlockItem(string upgradeId) {
            if (!UnlockedItems.Add(upgradeId)) {
                Debug.LogWarning($"Item {upgradeId} is already unlocked");
            }
        }
        
        public bool IsItemUnlocked(string upgradeId) {
            return UnlockedItems.Contains(upgradeId);
        }
    }
}