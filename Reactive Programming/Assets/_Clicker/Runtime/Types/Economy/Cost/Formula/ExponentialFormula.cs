using System;
using Types.Values;
using UnityEngine;

namespace Types.Modifiers.Cost.Formula {
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp")]
    public class ExponentialFormula : IFormula {
        [Tooltip("Starting value before exponential growth is applied.")]
        public double BaseValue;
        [Tooltip("Growth multiplier applied once per input step.")]
        public double Rate;
        
        public Value Evaluate(double input) {
            if (BaseValue <= 0d) return Value.Zero;
            if (Rate <= 0d) return input <= 0d ? new Value(BaseValue) : Value.Zero;

            return Value.FromLog10(Math.Log10(BaseValue) + input * Math.Log10(Rate));
        }
    }
}
