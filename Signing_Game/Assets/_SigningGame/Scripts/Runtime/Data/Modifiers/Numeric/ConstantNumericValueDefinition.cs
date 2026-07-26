using System;
using UnityEngine;
using Utils;

namespace Data.Modifiers.Numeric {
    [Serializable]
    public class ConstantNumericValueDefinition : NumericValueDefinition {

        [SerializeField] private Value _value;
        public override Value Evaluate(IModifierContext context) {
            return _value;
        }
    }
}