using System;
using Types.Values;
using UnityEngine;

namespace Types.Modifiers.Cost.Formula {
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp")]
    public class ExponentialFormula : IFormula {
        [Tooltip("Starting value before exponential growth is applied.")]
        public Value BaseValue;
        [Tooltip("Growth multiplier applied once per input step.")]
        public Value Rate;
        
        public Value Evaluate(Value input) {
            if (BaseValue <= Value.Zero) return Value.Zero;
            if (Rate <= Value.Zero) return input <= Value.Zero ? BaseValue : Value.Zero;

            return Value.FromLog10(BaseValue.ToLog10() + input.ToDouble() * Rate.ToLog10());
        }
    }
}
