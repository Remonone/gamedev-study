using System;
using Types.Enums.Values;
using UnityEngine;

namespace Types.Enums.Cost.Formula {
    [Serializable]
    public class AccumulationFormula : IFormula {
        
        [SerializeReference] public IFormula[] Formulas;
        
        public Value Evaluate(double input) {
            var value = Value.Zero;
            foreach (var formula in Formulas) {
                value += formula.Evaluate(input);
            }

            return value;
        }
    }
}
