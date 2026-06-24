using System;
using Types.Values;
using UnityEngine;

namespace Types.Modifiers.Cost.Formula {
    [Serializable]
    public class StepFormula : IFormula {
        
        [Tooltip("Starting value before step scaling is applied.")]
        public double BaseValue;
        [Tooltip("Input interval used for one step. Values below 1 are treated as 1.")]
        public double StepOn;
        [Tooltip("Amount added for each StepOn units of input.")]
        public double StepOf;
        
        public Value Evaluate(double input) {
            return new Value(BaseValue) + new Value(input / Math.Max(1d, StepOn) * StepOf);
        }
    }
}
