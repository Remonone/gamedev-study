using Bases.Buildings;
using Types;

namespace Types.Economy {
    [System.Serializable]
    public struct ModifierTarget {
        public ModifierTargetMode Mode;
        public StructureType StructureType;
        public string Name;

        public bool Matches(BuildingState building) {
            switch (Mode) {
                case ModifierTargetMode.All:
                    return true;
                case ModifierTargetMode.ByStructureType:
                    return building.Definition.Type == StructureType;
                case ModifierTargetMode.ByName:
                    return building.Definition.Name == Name;
                default:
                    return false;
            }
        }
    }
}
