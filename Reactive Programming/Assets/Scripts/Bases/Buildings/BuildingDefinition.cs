using Types;
using Types.Economy.Cost.Formula;
using UnityEngine;
using Utils.Properties;

namespace Bases.Buildings {
    [CreateAssetMenu(fileName = "Building", menuName = "Clicker/Building", order = 0)]
    public class BuildingDefinition : ScriptableObject {
        public string Name;
		[SerializeReference] public IFormula Cost;
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