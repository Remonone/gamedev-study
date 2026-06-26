using System;
using Types.Values;
using UnityEngine;

namespace Types.Modifiers.Cost.Formula {
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp")]
    public class PowerFormula : IFormula {
        [Tooltip("Coefficient multiplied by input raised to the configured power.")]
        public double BaseValue;
        [Tooltip("Exponent applied to the input value.")]
        public double Power;
        
        public Value Evaluate(double input) {
            if (BaseValue <= 0d) return Value.Zero;
            if (input <= 0d) return Power <= 0d ? new Value(BaseValue) : Value.Zero;

            return Value.FromLog10(Math.Log10(BaseValue) + Power * Math.Log10(input));
        }
    }
}
