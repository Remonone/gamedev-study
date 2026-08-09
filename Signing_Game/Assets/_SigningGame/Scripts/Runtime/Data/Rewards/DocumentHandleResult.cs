using Data.Enums;
using Data.Documents;

namespace Data.Rewards {
    public readonly struct DocumentHandleResult {
        public DocumentKind Kind { get; }
        public RewardStatus Status { get; }
        public float Accuracy { get; }

        public DocumentHandleResult(DocumentKind kind, RewardStatus status, float accuracy) {
            Kind = kind;
            Status = status;
            Accuracy = accuracy;
        }
    }
}
