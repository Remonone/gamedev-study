using System;
using System.Linq;
using UnityEngine;
using Utils;

namespace Data.Formulas {
    [Serializable]
    public class AccumulationFormula : IFormula {
        
        [SerializeReference, Tooltip("Nested formulas evaluated with the same input and added together")]
        public IFormula[] Formulas;
        
        public Value Evaluate(Value input) {
            return Formulas.Aggregate(Value.Zero, (current, formula) => current + formula.Evaluate(input));
        }
    }
}