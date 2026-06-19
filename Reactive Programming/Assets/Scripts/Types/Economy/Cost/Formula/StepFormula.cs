using System;
using Types.Enums.Values;

namespace Types.Enums.Cost.Formula {
    [Serializable]
    public class StepFormula : IFormula {
        
        public double BaseValue;
        public double StepOn;
        public double StepOf;
        
        public Value Evaluate(double input) {
            return new Value(BaseValue) + new Value(input / Math.Max(1d, StepOn) * StepOf);
        }
    }
}
