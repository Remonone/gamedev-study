using UnityEngine;

namespace Data.Bills {
    [CreateAssetMenu(menuName = "Bills/Requirement Template", fileName = "Bill Requirement")]
    public sealed class BillRequirementTemplateDefinition : ScriptableObject {
        public string Id;
        public BillRequirementKind Kind;

        [Header("Numeric target")]
        public int MinimumTarget;
        public int MaximumTarget;

        [Header("Upgrade target")]
        public string UpgradeId;

        [Header("Balance at target range endpoints")]
        public BillRequirementBalance MinimumBalance;
        public BillRequirementBalance MaximumBalance;

        public BillRequirementBalance ResolveBalance(int target) {
            if (Kind == BillRequirementKind.OwnedUpgrade || MinimumTarget == MaximumTarget) {
                return MinimumBalance;
            }

            double t = (double)(target - MinimumTarget) / (MaximumTarget - MinimumTarget);
            return BillRequirementBalance.Lerp(MinimumBalance, MaximumBalance, t);
        }
    }
}
