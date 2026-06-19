using System;
using Types.Enums.Values;
using UnityEngine;

namespace Types.Enums.Cost.Formula {
    [Serializable]
    public class LinearFormula : IFormula {

        [Tooltip("Starting value added before input scaling.")]
        public double BaseValue;
        [Tooltip("Amount added for each input unit.")]
        public double Multiplier;
        
        public Value Evaluate(double input) {
            return new Value(BaseValue) + new Value(Multiplier * input);
        }
    }
}
