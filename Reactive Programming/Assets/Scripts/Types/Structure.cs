using System;
using Types.Enums.Buildings;
using UnityEngine;

namespace Types.Enums {
     public class Structure : MonoBehaviour, IStructure {
        [SerializeField, Tooltip("Scene label for this structure. Runtime interaction type comes from Definition.Type.")]
        private GovernmentInteractionType _type;
        [SerializeField, Tooltip("Building definition used to create runtime state for this structure.")]
        private BuildingDefinition _definition;

        private BuildingState _state;
        
        public BuildingState State => _state;

        private void Awake() {
            _state = new BuildingState(_definition, 1);
        }
     }
}
