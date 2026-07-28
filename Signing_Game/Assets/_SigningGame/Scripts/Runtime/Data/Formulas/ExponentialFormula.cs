using System;
using Utils;

namespace Data.Formulas {
    [Serializable]
    public class ExponentialFormula {

        public Value StartValue;
        public Value Pattern;
        
        public Value Evaluate(Value input) {
            // eq.: StartValue * (Pattern ^ input)
            return Value.FromLog10(StartValue.ToLog10() + input.ToDouble() * Pattern.ToLog10());
        }
    }
}