using System.Collections.Generic;
using Bases.Buildings;
using UnityEngine;

namespace Services {
    public class BuildingWatcherService : IService {
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

    }
}