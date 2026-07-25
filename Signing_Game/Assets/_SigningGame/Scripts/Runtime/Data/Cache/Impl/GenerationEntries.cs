using Utils.Attributes;

namespace Data.Cache {
    [CacheEntryGroup("Generation")]
    public struct GenerationEntries {
        [ModifiableParameter("TokenPerSecond")]
        public float TokenPerSecond;
        [ModifiableParameter("DispenseCooldown")]
        public float DispenseCooldown;
        public GenerationEntries(float tokenPerSecond, float dispenseCooldown) {
            TokenPerSecond = tokenPerSecond;
            DispenseCooldown = dispenseCooldown;
        }
    }
}