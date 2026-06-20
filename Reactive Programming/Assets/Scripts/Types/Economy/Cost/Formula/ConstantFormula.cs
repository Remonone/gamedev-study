using System;
using Types.Modifiers.Definitions.Values;
using UnityEngine;

namespace Types.Modifiers.Definitions.Cost.Formula {
    [Serializable]
    public class ConstantFormula : IFormula {

        [Tooltip("Value returned for every input.")]
        public double BaseValue;
        
        public Value Evaluate(double input) {
            return new Value(BaseValue);
        }
    }
}
