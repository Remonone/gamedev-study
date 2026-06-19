using Types.Enums.Values;

namespace Types.Enums.Cost.Formula {
    public interface IFormula {
        public Value Evaluate(double input);
    }
}
