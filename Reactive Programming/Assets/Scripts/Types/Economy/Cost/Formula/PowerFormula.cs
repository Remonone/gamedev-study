using System;

namespace Types.Enums.Cost.Formula {
    [Serializable]
    public class PowerFormula : IFormula {
        public double BaseValue;
        public double Power;
        
        public decimal Evaluate(decimal input) {
            return (decimal)BaseValue * (decimal)Math.Pow((double)input, Power);
        }
    }
}
