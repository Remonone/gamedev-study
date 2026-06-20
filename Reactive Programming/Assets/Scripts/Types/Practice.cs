using System.Collections.Generic;
using Types.Modifiers.Definitions.Buildings;
using UnityEngine;

namespace Types.Modifiers.Definitions {
    [CreateAssetMenu(fileName = "Artifact", menuName = "Clicker/Artifact", order = 0)]
    public class Practice : ScriptableObject {
        
        public void ApplyRules(BuildingState building, List<StatModifier> modifiers) {
        }
    }
}