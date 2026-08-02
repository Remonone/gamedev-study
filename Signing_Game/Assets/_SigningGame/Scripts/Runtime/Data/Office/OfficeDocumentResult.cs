using Utils;

namespace Data.Office {
    public readonly struct OfficeDocumentResult {
        public int ClerkId { get; }
        public bool Accepted { get; }
        public float Quality { get; }
        public Value RequestedReward { get; }
        public Value CreditedReward { get; }

        public OfficeDocumentResult(int clerkId, bool accepted, float quality, Value requestedReward,
            Value creditedReward) {
            ClerkId = clerkId;
            Accepted = accepted;
            Quality = quality;
            RequestedReward = requestedReward;
            CreditedReward = creditedReward;
        }
    }
}
