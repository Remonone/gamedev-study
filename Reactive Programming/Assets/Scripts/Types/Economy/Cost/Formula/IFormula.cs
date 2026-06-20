using Types.Modifiers.Definitions.Values;

namespace Types.Modifiers.Definitions.Cost.Formula {
    public interface IFormula {
        public Value Evaluate(double input);
    }
}
