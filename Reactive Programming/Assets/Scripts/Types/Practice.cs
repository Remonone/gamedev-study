using System.Collections.Generic;
using Types.Enums.Buildings;
using UnityEngine;

namespace Types.Enums {
    [CreateAssetMenu(fileName = "Artifact", menuName = "Clicker/Artifact", order = 0)]
    public class Practice : ScriptableObject {
        
        public void ApplyRules(BuildingState building, List<StatModifier> modifiers) {
        }
    }
}