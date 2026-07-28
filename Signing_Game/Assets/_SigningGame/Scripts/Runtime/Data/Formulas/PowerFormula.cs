using System;
using UnityEngine;
using Utils;

namespace Data.Formulas {
    [Serializable]
    public class QuadranticFormula : IFormula {
        [Tooltip("Coefficient multiplied by input raised to the configured power")]
        public Value Coefficient;
        [Tooltip("Exponent applied to the input value")]
        public Value Power;

        public Value Evaluate(Value input) {
            // eq.: Coefficient * (input ^ Power)
            return Value.FromLog10(Coefficient.ToLog10() + input.ToLog10() * Power.ToDouble());
        }
    }
}