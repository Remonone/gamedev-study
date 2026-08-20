using System;
using UnityEngine.Serialization;
using UnityEngine;

namespace Data.Bills {
    [CreateAssetMenu(menuName = "Bills/Requirement Template", fileName = "Bill Requirement")]
    public sealed class BillRequirementTemplateDefinition : ScriptableObject {
        public string Id;

        [SerializeReference] public BillRequirementDefinition Definition;

        public BillRequirementDefinition Requirement => Definition;

        [Header("Presentation")]
        public string DisplayName;
        [TextArea] public string ShortDescription;
        public Color Color = Color.white;

        [Header("Balance at target range endpoints")]
        public BillRequirementBalance MinimumBalance;
        public BillRequirementBalance MaximumBalance;

        public BillRequirementBalance ResolveBalance(double t) {
            t = Math.Clamp(t, 0d, 1d);
            return BillRequirementBalance.Lerp(MinimumBalance, MaximumBalance, t);
        }
    }
}
