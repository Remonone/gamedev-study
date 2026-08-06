using System;
using UnityEngine;
using Utils.Attributes;

namespace Data.Cache {
    [Serializable, CacheEntryGroup("Generation")]
    public struct GenerationEntries {
        [ModifiableParameter("TokenPerSecond", Minimum = 0d)]
        public float TokenPerSecond;
        [ModifiableParameter("DispenseCooldown", Minimum = 0d)]
        public float DispenseCooldown;
        public GenerationEntries(float tokenPerSecond, float dispenseCooldown) {
            TokenPerSecond = tokenPerSecond;
            DispenseCooldown = dispenseCooldown;
        }
    }
    [CreateAssetMenu(menuName = "References/Generation Reference")]
    public class GenerationReference : BaseEntries<GenerationEntries> { }
}
