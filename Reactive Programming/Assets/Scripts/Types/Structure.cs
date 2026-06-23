using System;
using Services;
using Services.Components;
using Types.Modifiers.Definitions.Buildings;
using UnityEngine;

namespace Types.Modifiers.Definitions {
     public class Structure : MonoBehaviour {
        [SerializeField, Tooltip("Building definition used to create runtime state for this structure.")]
        private BuildingDefinition _definition;

        private BuildingState _state;
        
        public BuildingState State => _state;
        public BuildingDefinition Definition => _definition;

        private void Start() {
            _state = ServiceLocator.Instance.GetService<BuildingWatcherService>().GetBuildingState(_definition.Name);
            _state.IsDirty = true;
        }
     }
}
