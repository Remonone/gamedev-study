using System;
using UnityEngine;

namespace Types.Economy.Cost.Formula {
    [Serializable]
    public class AccumulationFormula : IFormula {
        
        [SerializeReference] public IFormula[] Formulas;
        
        public decimal Evaluate(decimal input) {
            decimal value = 0;
            foreach (var formula in Formulas) {
                value += formula.Evaluate(input);
            }

            return value;
        }
    }
}