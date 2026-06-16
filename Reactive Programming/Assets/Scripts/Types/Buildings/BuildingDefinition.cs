using Types.Enums.Cost;
using Types.Enums.Cost.Formula;
using UnityEngine;

namespace Types.Enums.Buildings {
    [CreateAssetMenu(fileName = "Building", menuName = "Clicker/Building", order = 0)]
    public class BuildingDefinition : ScriptableObject {
        public string Name;
        [SerializeField] [TextArea(2, 4)] public string Description;
        [SerializeField] public Sprite Icon;
		[SerializeField] public CostResolver Cost;
        public StructureType Type;
        [SerializeReference] public IFormula Income;
        [SerializeReference] public IFormula Frequency;
        [SerializeReference] public IFormula StabilityModifier;
        [SerializeReference] public IFormula StabilityModifierMultiplier;
        [SerializeReference] public IFormula MultiplierCoefficient;
        [SerializeReference] public IFormula CriticalChance;
        [SerializeReference] public IFormula CriticalMultiplier;
    }
}
