using System;
using Types.Enums.Values;
using UnityEngine;

namespace Types.Enums.Cost.Formula {
    [Serializable]
    public class ConstantFormula : IFormula {

        [Tooltip("Value returned for every input.")]
        public double BaseValue;
        
        public Value Evaluate(double input) {
            return new Value(BaseValue);
        }
    }
}
