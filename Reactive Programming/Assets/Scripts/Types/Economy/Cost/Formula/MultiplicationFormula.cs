using System;

namespace Types.Enums.Cost.Formula {
    
    [Serializable]
    public class MultiplicationFormula : IFormula {
        
        public IFormula[] Formulas;
        
        public decimal Evaluate(decimal input) {
            decimal value = 1;
            foreach (var formula in Formulas) {
                value *= formula.Evaluate(input);
            }
            
            return value;
        }
    }
}