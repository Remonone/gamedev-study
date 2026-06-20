using System.Collections.Generic;
using System.Linq;
using Types.Modifiers.Definitions.Buildings;
using Newtonsoft.Json.Linq;
using Save;
using UnityEngine;

namespace Services {
    public class BuildingWatcherService : IService, ISaveable {
        private readonly Dictionary<string, BuildingState> _buildingsByName;
        
        public IReadOnlyDictionary<string, BuildingState> BuildingsByName => _buildingsByName;

        public BuildingWatcherService(List<BuildingDefinition> definitions) {
            _buildingsByName = new Dictionary<string, BuildingState>();
            foreach (var definition in definitions) {
                if (_buildingsByName.ContainsKey(definition.Name)) {
                    Debug.LogError($"Duplicate building name: {definition.Name}");
                    continue;
                }
                var buildingState = new BuildingState(definition, 0);
                _buildingsByName.Add(definition.Name, buildingState);
            }
            Debug.Log($"Initialized {definitions.Count} buildings");
        }

        public BuildingState GetBuildingState(string name) {
            if (_buildingsByName.TryGetValue(name, out var buildingState)) {
                return buildingState;
            }
            Debug.LogWarning($"Building not found: {name}");
            return null;
        }
        
        
        public void RegisterBuilding(params BuildingDefinition[] buildings) {
            foreach (var definition in buildings) {
                if (_buildingsByName.ContainsKey(definition.Name)) {
                    Debug.LogError($"Duplicate building name: {definition.Name}");
                    continue;
                }
                var buildingState = new BuildingState(definition, 0);
                _buildingsByName.Add(definition.Name, buildingState);
            }
            Debug.Log($"Registered {buildings.Length} buildings");
        }

        public string SaveKey => "Buildings";
        public int Priority => 100;
        
        public JToken Save() {
            return new JObject(
                    new JProperty("buildings", new JArray(
                        from building in _buildingsByName
                        select new JObject(
                            new JProperty("name", building.Key),
                            new JProperty("level", building.Value.Level)
                        )
                    )
                    )
                );
        }

        public void Load(JToken data) {
            foreach (var building in data["buildings"]) {
                var key = building.Value<string>("name");
                var level = building.Value<int>("level");
                if (_buildingsByName.TryGetValue(key, out var buildingState)) {
                    buildingState.Level = level;
                    buildingState.IsDirty = true;
                }
            }
        }
    }
}