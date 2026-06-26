using System;
using Types.Values;
using UnityEngine;

namespace Types.Modifiers.Cost.Formula {
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp")]
    public class AccumulationFormula : IFormula {
        
        [SerializeReference, Tooltip("Nested formulas evaluated with the same input and added together.")]
        public IFormula[] Formulas;
        
        public Value Evaluate(double input) {
            var value = Value.Zero;
            foreach (var formula in Formulas) {
                value += formula.Evaluate(input);
            }

            return value;
        }
    }
}
