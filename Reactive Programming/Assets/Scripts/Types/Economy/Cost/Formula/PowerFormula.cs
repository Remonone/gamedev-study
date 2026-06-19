using System;
using Types.Enums.Values;

namespace Types.Enums.Cost.Formula {
    [Serializable]
    public class PowerFormula : IFormula {
        public double BaseValue;
        public double Power;
        
        public Value Evaluate(double input) {
            if (BaseValue <= 0d) return Value.Zero;
            if (input <= 0d) return Power <= 0d ? new Value(BaseValue) : Value.Zero;

            return Value.FromLog10(Math.Log10(BaseValue) + Power * Math.Log10(input));
        }
    }
}
