using System;
using Types.Values;
using UnityEngine;

namespace Types.Modifiers.Cost.Formula {
    [Serializable]
    public class StepCurve : IFormula {
        
        [Tooltip("Base value multiplied by the curve result.")]
        public double BaseValue;
        [Tooltip("Multiplier curve sampled by the formula input.")]
        public AnimationCurve Curve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        
        public Value Evaluate(double input) {
            return new Value(BaseValue) * Curve.Evaluate((float)input);
        }
    }
}
