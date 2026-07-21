using Data.Enums;
using Utils;

namespace Data.Rewards {
    public readonly struct RewardResult {
        public readonly RewardStatus Status;
        public readonly RewardKind Kind;
        public readonly Value Amount;
        public readonly float Accuracy;
        
        public RewardResult(RewardStatus status, RewardKind kind, Value amount, float accuracy) {
            Status = status;
            Kind = kind;
            Amount = amount;
            Accuracy = accuracy;
        }
    }
}