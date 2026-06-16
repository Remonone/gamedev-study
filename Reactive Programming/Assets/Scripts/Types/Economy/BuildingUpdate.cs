using Types.Enums.Buildings;

namespace Types.Enums {
    public readonly struct BuildingUpdate {
        public readonly BuildingState Building;
        public readonly ComputedStats Stats;

        public BuildingUpdate(BuildingState state, ComputedStats stats) {
            Building = state;
            Stats = stats;
        }
    }
}