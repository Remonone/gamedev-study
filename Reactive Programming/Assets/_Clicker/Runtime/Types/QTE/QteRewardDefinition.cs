using System;
using System.Collections.Generic;
using Types.Enums;
using UnityEngine;
using Random = System.Random;

namespace Types.QTE {
    [Serializable]
    public class QteRewardDefinition {
        [SerializeField, Tooltip("Resource categories this QTE reward can choose from at spawn time.")]
        private GovernmentInteractionType[] _eligibleResources;
        [SerializeField, Min(0f), Tooltip("Reward per click is current selected resource amount multiplied by this value.")]
        private double _currentAmountMultiplier = 0.01d;

        public IReadOnlyList<GovernmentInteractionType> EligibleResources => _eligibleResources;
        public double CurrentAmountMultiplier => Math.Max(0d, _currentAmountMultiplier);

        public bool TrySelectResource(Random rng, out GovernmentInteractionType resource) {
            resource = default;
            if (rng == null || _eligibleResources == null || _eligibleResources.Length == 0) {
                return false;
            }

            resource = _eligibleResources[rng.Next(0, _eligibleResources.Length)];
            return true;
        }
    }
}
