using System;

namespace Types.Enums.Cost.Formula {
    [Serializable]
    public class StepFormula : IFormula {
        
        public double BaseValue;
        public double StepOn;
        public double StepOf;
        
        public decimal Evaluate(decimal input) {
            return (decimal)BaseValue + (input / (decimal)Math.Max(1d, StepOn)) * (decimal)StepOf;
        }
    }
}
