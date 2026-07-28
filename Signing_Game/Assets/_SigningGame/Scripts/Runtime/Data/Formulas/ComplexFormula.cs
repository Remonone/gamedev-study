using System;
using UnityEngine;
using Utils;

namespace Data.Formulas {
    [Serializable]
    public class ComplexFormula : IFormula {
        [SerializeReference, Tooltip("Formulas evaluated in order; each result becomes the next formula input, starting from input value")]
        public IFormula[] Formulas;
        
        public Value Evaluate(Value input) {
            var result = input;
            foreach (var formula in Formulas) {
                result = formula.Evaluate(result);
            }
            return result;
        }
    }
}