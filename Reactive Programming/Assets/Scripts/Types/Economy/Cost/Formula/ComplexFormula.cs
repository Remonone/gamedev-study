using System;
using UnityEngine;

namespace Types.Economy.Cost.Formula {
    [Serializable]
    public class ComplexFormula : IFormula {
        
        [SerializeReference]
        public IFormula[] Formulas = Array.Empty<IFormula>();
        
        public decimal Evaluate(decimal input) {
            var value = 0m;
            foreach (var formula in Formulas) {
                value = formula.Evaluate(value);
            }
            return value;
        }
    }
}
