using System.Collections.Generic;
using UnityEngine;
using Utils;

namespace Data.Modifiers {
    [CreateAssetMenu(menuName = "Modifiers/Modifier", fileName = "Modifier")]
    public class ModifierDefinition : ScriptableObject {
        public List<NumericModifierDefinition> NumericModifiers;
    }
}