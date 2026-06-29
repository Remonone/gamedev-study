using System;
using Types.Values;
using UnityEngine;

namespace Types.Modifiers.Cost.Formula {
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp")]
    public class PowerFormula : IFormula {
        [Tooltip("Coefficient multiplied by input raised to the configured power.")]
        public Value BaseValue;
        [Tooltip("Exponent applied to the input value.")]
        public Value Power;
        
        public Value Evaluate(Value input) {
            if (BaseValue <= Value.Zero) return Value.Zero;
            if (input <= Value.Zero) return Power <= Value.Zero ? BaseValue : Value.Zero;

            return Value.FromLog10(BaseValue.ToLog10() + Power.ToDouble() * input.ToLog10());
        }
    }
}
