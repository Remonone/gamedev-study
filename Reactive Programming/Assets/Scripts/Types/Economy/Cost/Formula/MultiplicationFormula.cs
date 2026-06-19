using System;
using Types.Enums.Values;

namespace Types.Enums.Cost.Formula {
    
    [Serializable]
    public class MultiplicationFormula : IFormula {
        
        public IFormula[] Formulas;
        
        public Value Evaluate(double input) {
            var value = Value.One;
            foreach (var formula in Formulas) {
                value *= formula.Evaluate(input);
            }
            
            return value;
        }
    }
}
