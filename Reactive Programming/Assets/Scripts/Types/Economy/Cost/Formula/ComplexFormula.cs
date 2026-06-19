using System;
using Types.Enums.Values;
using UnityEngine;

namespace Types.Enums.Cost.Formula {
    [Serializable]
    public class ComplexFormula : IFormula {
        
        [SerializeReference]
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
