using System;

namespace Types.Enums.Cost.Formula {
    [Serializable]
    public class LinearFormula : IFormula {

        public double BaseValue;
        public double Multiplier;
        
        public decimal Evaluate(decimal input) {
            return (decimal)BaseValue + (decimal)Multiplier * input;
        }
    }
}
