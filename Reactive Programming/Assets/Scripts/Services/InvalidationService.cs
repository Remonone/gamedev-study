using System.Collections.Generic;
using Types.Buildings;
using Types;
using Types.Economy;
using Types.Economy.Modifiers.Target;

namespace Services {
    public class InvalidationService : IService {
        private readonly IReadOnlyDictionary<string, BuildingState> _buildings;

        public InvalidationService(IReadOnlyDictionary<string, BuildingState> buildings) {
            _buildings = buildings;
        }
        
        public void InvalidateBuilding(string name) {
            if (!_buildings.ContainsKey(name)) {
                return;
            }
            _buildings[name].IsDirty = true;
        }

        public void InvalidateBuilding(BuildingState building) {
            building.IsDirty = true;
        }
        
        public void InvalidateAll() {
            foreach (var building in _buildings.Values) {
                building.IsDirty = true;
            }
        }

        public void InvalidateByStructureType(StructureType type) {
            foreach (var building in _buildings.Values) {
                if (building.Definition.Type == type) {
                    building.IsDirty = true;
                }
            }
        }

        public void MarkDirtyByTarget(ModifierTarget targetDeprecated) {
            foreach (var building in _buildings.Values) {
                if (targetDeprecated.Matches(building)) {
                    building.IsDirty = true;
                }
            }
        }
        
    }
}