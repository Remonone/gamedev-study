using System;
using Types.Enums.Values;

namespace Types.Enums.Cost.Formula {
    [Serializable]
    public class ExponentialFormula : IFormula {
        public double BaseValue;
        public double Rate;
        
        public Value Evaluate(double input) {
            if (BaseValue <= 0d) return Value.Zero;
            if (Rate <= 0d) return input <= 0d ? new Value(BaseValue) : Value.Zero;

            return Value.FromLog10(Math.Log10(BaseValue) + input * Math.Log10(Rate));
        }
    }
}
