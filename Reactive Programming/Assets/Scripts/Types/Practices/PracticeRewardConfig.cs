using System;
using System.Collections.Generic;
using System.Linq;
using Types;
using UnityEngine;

namespace Types.Practices {
    [CreateAssetMenu(fileName = "PracticeRewardConfig", menuName = "Clicker/Practices/Reward Config", order = 0)]
    public class PracticeRewardConfig : ScriptableObject {
        [SerializeField, Min(1), Tooltip("How many practice choices are shown in the research reward popup.")]
        private int _choicesCount = 3;
        [SerializeField, Tooltip("Recycle power per rarity. Code converts power to concrete stat values.")]
        private List<PracticeRecyclePower> _recyclePowers = new();

        public int ChoicesCount => Mathf.Max(1, _choicesCount);

        public float GetRecyclePower(PracticeRarity rarity) {
            var entry = _recyclePowers.FirstOrDefault(item => item.Rarity == rarity);
            if (entry != null) {
                return Mathf.Max(0f, entry.Power);
            }

            return rarity switch {
                PracticeRarity.Common => 0.05f,
                PracticeRarity.Good => 0.08f,
                PracticeRarity.Unusual => 0.12f,
                PracticeRarity.Advanced => 0.18f,
                PracticeRarity.Brilliant => 0.3f,
                _ => 0.05f
            };
        }
    }

    [Serializable]
    public class PracticeRecyclePower {
        public PracticeRarity Rarity;
        [Min(0f), Tooltip("Abstract recycle strength. Conversion depends on selected stat.")]
        public float Power = 0.05f;
    }

    [Serializable]
    public class PracticeRarityWeight {
        public PracticeRarity Rarity;
        [Min(0f), Tooltip("Weight used when research determines reward rarity.")]
        public float Weight = 1f;
    }

    public struct ResearchPracticeModifiers {
        public float PointsPerSecondMultiplier;
        public float RequiredPointsMultiplier;
        public float FlatPointsPerSecond;

        public static ResearchPracticeModifiers Default => new ResearchPracticeModifiers {
            PointsPerSecondMultiplier = 1f,
            RequiredPointsMultiplier = 1f,
            FlatPointsPerSecond = 0f
        };
    }
}
