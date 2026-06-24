using Types.Values;

namespace Types.Modifiers.Cost.Formula {
    public interface IFormula {
        public Value Evaluate(double input);
    }
}
