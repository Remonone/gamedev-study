using System;
using Types.Modifiers.Definitions.Values;
using UnityEngine;

namespace Types.Modifiers.Definitions.Cost.Formula {
    [Serializable]
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
