using System.Collections.Generic;
using Types.Buildings;
using UnityEngine;

namespace Types.Modifiers.Target {
    [CreateAssetMenu(fileName = "CompositeTarget", menuName = "Clicker/Modifiers/Target/Composite Target", order = 0)]
    public class CompositeTarget : ModifierTarget {
        
        [SerializeField] private List<ModifierTarget> _targets;
        
        public override bool Matches(BuildingState building) {
            foreach (var target in _targets) {
                if (target.Matches(building)) return true;
            }

            return false;
        }
    }
}