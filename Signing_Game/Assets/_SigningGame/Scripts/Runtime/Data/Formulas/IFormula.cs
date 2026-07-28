using Utils;

namespace Data.Formulas {
    public interface IFormula {
        public Value Evaluate(Value input);
    }
}