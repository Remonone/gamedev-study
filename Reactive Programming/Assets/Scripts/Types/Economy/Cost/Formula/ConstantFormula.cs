using System;
using Types.Enums.Values;

namespace Types.Enums.Cost.Formula {
    [Serializable]
    public class ConstantFormula : IFormula {

        public double BaseValue;
        
        public Value Evaluate(double input) {
            return new Value(BaseValue);
        }
    }
}
