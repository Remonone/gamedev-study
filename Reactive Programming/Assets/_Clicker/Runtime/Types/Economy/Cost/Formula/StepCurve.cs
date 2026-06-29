using System;
using Types.Values;
using UnityEngine;

namespace Types.Modifiers.Cost.Formula {
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp")]
    public class StepCurve : IFormula {
        
        [Tooltip("Base value multiplied by the curve result.")]
        public Value BaseValue;
        [Tooltip("Multiplier curve sampled by the formula input.")]
        public AnimationCurve Curve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        
        public Value Evaluate(Value input) {
            return BaseValue * Curve.Evaluate(input.ToSingle());
        }
    }
}
