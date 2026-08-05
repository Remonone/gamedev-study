using Utils;

namespace Presentation {
    public enum OfficeSlotState {
        Clerk,
        Purchase,
        Vacant
    }

    public sealed class OfficeSlotPresentationModel {
        public OfficeSlotState State { get; }
        public int ClerkId { get; }
        public string Name { get; }
        public int Age { get; }
        public double BaseEfficiency { get; }
        public double BonusEfficiency { get; }
        public Value ReviewCost { get; }
        public bool CanReview { get; }
        public bool IsReviewPending { get; }
        public Value Bid { get; }
        public bool CanHire { get; }

        private OfficeSlotPresentationModel(
            OfficeSlotState state,
            int clerkId,
            string name,
            int age,
            double baseEfficiency,
            double bonusEfficiency,
            Value reviewCost,
            bool canReview,
            bool isReviewPending,
            Value bid,
            bool canHire) {
            State = state;
            ClerkId = clerkId;
            Name = name;
            Age = age;
            BaseEfficiency = baseEfficiency;
            BonusEfficiency = bonusEfficiency;
            ReviewCost = reviewCost;
            CanReview = canReview;
            IsReviewPending = isReviewPending;
            Bid = bid;
            CanHire = canHire;
        }

        public static OfficeSlotPresentationModel Clerk(
            int clerkId,
            string name,
            int age,
            double baseEfficiency,
            double bonusEfficiency,
            Value reviewCost,
            bool canReview,
            bool isReviewPending) {
            return new OfficeSlotPresentationModel(
                OfficeSlotState.Clerk,
                clerkId,
                name,
                age,
                baseEfficiency,
                bonusEfficiency,
                reviewCost,
                canReview,
                isReviewPending,
                Value.Zero,
                false);
        }

        public static OfficeSlotPresentationModel Purchase(Value bid, bool canHire) {
            return new OfficeSlotPresentationModel(
                OfficeSlotState.Purchase,
                0,
                null,
                0,
                0d,
                0d,
                Value.Zero,
                false,
                false,
                bid,
                canHire);
        }

        public static OfficeSlotPresentationModel Vacant() {
            return new OfficeSlotPresentationModel(
                OfficeSlotState.Vacant,
                0,
                null,
                0,
                0d,
                0d,
                Value.Zero,
                false,
                false,
                Value.Zero,
                false);
        }
    }
}
