using Types.Values;

namespace Types.Modifiers.Cost.Formula {
    public class SubtractionFormula : IFormula {
        public IFormula[] Formulas;
        public Value Evaluate(double input) {
            if (Formulas.Length < 1) {
                return Value.Zero;
            }

            Value value = Formulas[0].Evaluate(input);
            for (var i = 1; i < Formulas.Length; i++) {
                var result = value - Formulas[i].Evaluate(input);
                if(!result.HasValue) return Value.Zero;
                value = result.Value;
            }
            return value;
        }
    }
}