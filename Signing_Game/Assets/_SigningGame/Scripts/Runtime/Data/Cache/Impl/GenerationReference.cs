using System;
using UnityEngine;
using Utils.Attributes;

namespace Data.Cache {
    [Serializable, CacheEntryGroup("Generation")]
    public struct GenerationEntries {
        [ModifiableParameter("TokenPerSecond", Minimum = 0d)]
        public float TokenPerSecond;
        [ModifiableParameter("TokenPerIncome", Minimum = 0d)]
        public int TokenPerIncome;
        public GenerationEntries(float tokenPerSecond, int tokenPerIncome) {
            TokenPerSecond = tokenPerSecond;
            TokenPerIncome = tokenPerIncome;
        }
    }
    [CreateAssetMenu(menuName = "References/Generation Reference")]
    public class GenerationReference : BaseEntries<GenerationEntries> { }
}
