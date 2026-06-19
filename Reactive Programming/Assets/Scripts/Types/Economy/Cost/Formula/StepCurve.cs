using System;
using Types.Enums.Values;
using UnityEngine;

namespace Types.Enums.Cost.Formula {
    [Serializable]
    public class StepCurve : IFormula {
        
        public double BaseValue;
        public AnimationCurve Curve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        
        public Value Evaluate(double input) {
            return new Value(BaseValue) * Curve.Evaluate((float)input);
        }
    }
}
