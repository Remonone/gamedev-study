using System;
using System.Collections.Generic;
using UnityEngine;

namespace Data.Modifiers {
    [CreateAssetMenu(menuName = "Modifiers/Modifier", fileName = "Modifier")]
    public class ModifierDefinition : ScriptableObject {
        public List<NumericModifierDefinition> NumericModifiers;

        public List<Type> GetAffectedTypes() {
            var affectedTypes = new List<Type>();
            if (NumericModifiers == null) return affectedTypes;

            for (int index = 0; index < NumericModifiers.Count; index++) {
                NumericModifierDefinition modifier = NumericModifiers[index];
                if (modifier == null) continue;
                affectedTypes.Add(modifier.GetGroupType());
            }

            return affectedTypes;
        }
    }
}
