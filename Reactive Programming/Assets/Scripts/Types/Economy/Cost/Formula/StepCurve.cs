using System;
using UnityEngine;

namespace Types.Enums.Cost.Formula {
    [Serializable]
    public class StepCurve : IFormula {
        
        public double BaseValue;
        public AnimationCurve Curve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        
        public decimal Evaluate(decimal input) {
            return (decimal)BaseValue * (decimal)Curve.Evaluate((float)input);
        }
    }
}
