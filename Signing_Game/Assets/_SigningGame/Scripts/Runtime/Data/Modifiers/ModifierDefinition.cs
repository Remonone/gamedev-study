using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Data.Modifiers {
    [CreateAssetMenu(menuName = "Modifiers/Modifier", fileName = "Modifier")]
    public class ModifierDefinition : ScriptableObject {
        public List<NumericModifierDefinition> NumericModifiers;
        
        private List<Type> _affectedTypes;
        
        public List<Type> GetAffectedTypes() {
            if(_affectedTypes != null) return _affectedTypes;
            _affectedTypes = NumericModifiers.Select(modifier => modifier.GetGroupType()).ToList();
            return _affectedTypes;
        }
    }
}