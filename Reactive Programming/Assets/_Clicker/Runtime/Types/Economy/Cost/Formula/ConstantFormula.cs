using System;
using Types.Values;
using UnityEngine;

namespace Types.Modifiers.Cost.Formula {
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp")]
    public class ConstantFormula : IFormula {

        [Tooltip("Value returned for every input.")]
        public double BaseValue;
        
        public Value Evaluate(double input) {
            return new Value(BaseValue);
        }
    }
}
