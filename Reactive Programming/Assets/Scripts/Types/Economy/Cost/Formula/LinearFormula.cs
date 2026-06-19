using System;
using Types.Enums.Values;

namespace Types.Enums.Cost.Formula {
    [Serializable]
    public class LinearFormula : IFormula {

        public double BaseValue;
        public double Multiplier;
        
        public Value Evaluate(double input) {
            return new Value(BaseValue) + new Value(Multiplier * input);
        }
    }
}
