using System;
using Types.Values;
using UnityEngine;

namespace Types.Modifiers.Cost.Formula {
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp")]
    public class LinearFormula : IFormula {

        [Tooltip("Starting value added before input scaling.")]
        public Value BaseValue;
        [Tooltip("Amount added for each input unit.")]
        public Value Multiplier;
        
        public Value Evaluate(Value input) {
            return BaseValue + Multiplier * input;
        }
    }
}
