using System;
using Types.Modifiers.Definitions.Values;
using UnityEngine;

namespace Types.Modifiers.Definitions.Cost.Formula {
    [Serializable]
    public class ComplexFormula : IFormula {
        
        [SerializeReference, Tooltip("Formulas evaluated in order; each result becomes the next formula input, starting from zero.")]
        public IFormula[] Formulas = Array.Empty<IFormula>();
        
        public Value Evaluate(double input) {
            var value = Value.Zero;
            foreach (var formula in Formulas) {
                value = formula.Evaluate(value.ToDouble());
            }
            return value;
        }
    }
}
