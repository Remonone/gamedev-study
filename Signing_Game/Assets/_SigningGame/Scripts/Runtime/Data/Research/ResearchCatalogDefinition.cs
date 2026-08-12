using System;
using UnityEngine;
using Utils;

namespace Data.Research {
    [Serializable]
    public struct PracticeRarityDefinition {
        public string Id;
        public string DisplayName;
        public Color Color;
        [Min(1)] public int SelectionWeight;
        public Value SalePrice;
    }

    [CreateAssetMenu(menuName = "Research/Catalog", fileName = "Research Catalog")]
    public sealed class ResearchCatalogDefinition : ScriptableObject {
        public PracticeRarityDefinition[] Rarities = Array.Empty<PracticeRarityDefinition>();
        public PracticeDefinition[] Practices = Array.Empty<PracticeDefinition>();
    }
}
