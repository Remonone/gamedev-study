using Types;
using UnityEngine;
using Utils.Properties;

namespace Bases.Buildings {
    [CreateAssetMenu(fileName = "Building", menuName = "Clicker/Building", order = 0)]
    public class BuildingDefinition : ScriptableObject {
        public string Name;
		public IntProperty Cost;
        public StructureType Type;
        public IntProperty Income;
        public FloatProperty Frequency;
        public FloatProperty StabilityModifier;
        public FloatProperty StabilityModifierMultiplier;
        public FloatProperty MultiplierCoefficient;
        public FloatProperty CriticalChance;
        public FloatProperty CriticalMultiplier;
    }
}