using System;

namespace Types.Enums.Cost.Formula {
    [Serializable]
    public class ExponentialFormula : IFormula {
        public double BaseValue;
        public double Rate;
        
        public decimal Evaluate(decimal input) {
            return (decimal)BaseValue * (decimal)Math.Pow(Rate, (double)input);
        }
    }
}
