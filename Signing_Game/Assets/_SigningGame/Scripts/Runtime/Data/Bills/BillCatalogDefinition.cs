using System;
using UnityEngine;

namespace Data.Bills {
    [CreateAssetMenu(menuName = "Bills/Catalog", fileName = "Bill Catalog")]
    public sealed class BillCatalogDefinition : ScriptableObject {
        public BillRewardDefinition[] Rewards = Array.Empty<BillRewardDefinition>();
        public BillRequirementTemplateDefinition[] RequirementTemplates =
            Array.Empty<BillRequirementTemplateDefinition>();
    }
}
