using System.Collections.Generic;
using Types.Buildings;
using Types.Economy.Modifiers;
using UnityEngine;

namespace Types {
    [CreateAssetMenu(fileName = "Artifact", menuName = "Clicker/Artifact", order = 0)]
    public class Practice : ScriptableObject {
        
        public void ApplyRules(BuildingState building, List<StatModifier> modifiers) {
        }
    }
}