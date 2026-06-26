using System;
using Types.Values;
using UnityEngine;

namespace Types.Modifiers.Cost.Formula {
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp")]
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
