using System;
using Types.Enums.Buildings;
using UnityEngine;

namespace Types.Enums {
     public class Structure : MonoBehaviour, IStructure {
        [SerializeField] private GovernmentInteractionType _type;
        [SerializeField] private BuildingDefinition _definition;

        private BuildingState _state;
        
        public BuildingState State => _state;

        private void Awake() {
            _state = new BuildingState(_definition, 1);
        }
     }
}