using System;
using Types.Values;
using UnityEngine;

namespace Types.Modifiers.Cost.Formula {
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp")]
    public class StepFormula : IFormula {
        
        [Tooltip("Starting value before step scaling is applied.")]
        public Value BaseValue;
        [Tooltip("Input interval used for one step. Values below 1 are treated as 1.")]
        public Value StepOn;
        [Tooltip("Amount added for each StepOn units of input.")]
        public Value StepOf;
        
        public Value Evaluate(Value input) {
            var stepOn = Math.Max(1d, StepOn.ToDouble());
            return BaseValue + StepOf * (input.ToDouble() / stepOn);
        }
    }
}
