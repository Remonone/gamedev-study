using System;

namespace Types.Economy.Cost.Formula {
    [Serializable]
    public class ConstantFormula : IFormula {

        public double BaseValue;
        
        public decimal Evaluate(decimal input) {
            return (decimal)BaseValue;
        }
    }
}