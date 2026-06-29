using System.Collections.Generic;
using R3;
using Types.Buildings;
using Types.Enums;
using Types.Modifiers.Target;

namespace Services {
    public class InvalidationService : IService {
        private readonly IReadOnlyDictionary<string, BuildingState> _buildings;
        private readonly Subject<Unit> _invalidated = new();

        public Observable<Unit> Invalidated => _invalidated;

        public InvalidationService(IReadOnlyDictionary<string, BuildingState> buildings) {
            _buildings = buildings;
        }
        
        public void InvalidateBuilding(string name) {
            if (!_buildings.ContainsKey(name)) {
                return;
            }
            _buildings[name].IsDirty = true;
            PublishInvalidated();
        }

        public void InvalidateBuilding(BuildingState building) {
            building.IsDirty = true;
            PublishInvalidated();
        }
        
        public void InvalidateAll() {
            foreach (var building in _buildings.Values) {
                building.IsDirty = true;
            }
            PublishInvalidated();
        }

        public void InvalidateByStructureType(GovernmentInteractionType type) {
            foreach (var building in _buildings.Values) {
                if (building.Definition.Type == type) {
                    building.IsDirty = true;
                }
            }
            PublishInvalidated();
        }

        public void MarkDirtyByTarget(ModifierTarget targetDeprecated) {
            foreach (var building in _buildings.Values) {
                if (targetDeprecated.Matches(building)) {
                    building.IsDirty = true;
                }
            }
            PublishInvalidated();
        }

        private void PublishInvalidated() {
            _invalidated.OnNext(Unit.Default);
        }
        
    }
}
