using Types.Values;

namespace Types.Modifiers.Cost.Formula {
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp")]
    public class SubtractionFormula : IFormula {
        public IFormula[] Formulas;
        public Value Evaluate(Value input) {
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
