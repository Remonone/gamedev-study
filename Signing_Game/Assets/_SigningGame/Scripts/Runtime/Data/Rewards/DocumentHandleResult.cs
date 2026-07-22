using Data.Enums;

namespace Data.Rewards {
    public readonly struct DocumentHandleResult {
        public RewardStatus Status { get; }
        public float Accuracy { get; }

        public DocumentHandleResult(RewardStatus status, float accuracy) {
            Status = status;
            Accuracy = accuracy;
        }
    }
}