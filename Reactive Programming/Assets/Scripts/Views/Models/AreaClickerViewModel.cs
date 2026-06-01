using Components;
using Components.Instances;
using Types;
using UnityEngine;

namespace Views.Models {
    public class AreaClickerViewModel {

        private StructureClickService _structureService;

        public AreaClickerViewModel() {
            _structureService = ServiceLocator.Instance.GetService<StructureClickService>();
        }

        public void Click(StructureType structure) {
            _structureService.HandleStructureInteraction(structure);
        }
    }
}