using System;
using Types.Values;
using UnityEngine;

namespace Types.Modifiers.Cost.Formula {
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp")]
    public class ConstantFormula : IFormula {

        [Tooltip("Value returned for every input.")]
        public Value BaseValue;
        
        public Value Evaluate(Value input) {
            return BaseValue;
        }
    }
}
