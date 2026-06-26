using Types.Values;

namespace Types.Research {
    public readonly struct ResearchState {
        public readonly int CompletedCount;
        public readonly Value InvestedPoints;
        public readonly Value NextCost;
        public readonly Value PointsPerSecond;
        public readonly float ScaleModifier;
        public readonly bool IsUnlocked;
        public readonly bool CanComplete;
        public readonly float Progress01;

        public ResearchState(
            int completedCount,
            Value investedPoints,
            Value nextCost,
            Value pointsPerSecond,
            float scaleModifier,
            bool isUnlocked) {
            CompletedCount = completedCount;
            InvestedPoints = investedPoints;
            NextCost = nextCost;
            PointsPerSecond = pointsPerSecond;
            ScaleModifier = scaleModifier;
            IsUnlocked = isUnlocked;
            CanComplete = isUnlocked && investedPoints >= nextCost;

            var cost = nextCost.ToDouble();
            Progress01 = cost <= 0d ? 0f : UnityEngine.Mathf.Clamp01((float)(investedPoints.ToDouble() / cost));
        }
    }
}
