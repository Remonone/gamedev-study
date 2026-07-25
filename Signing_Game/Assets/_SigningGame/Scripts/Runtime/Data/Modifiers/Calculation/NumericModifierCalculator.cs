namespace Data.Modifiers.Calculation {
    public static class NumericModifierCalculator {
        public static double Apply(double currentValue, NumericModifierOperation operation, double operand) {
            return operation switch {
                NumericModifierOperation.Add => currentValue + operand,
                NumericModifierOperation.AddPercent => currentValue + (1d * operand),
                NumericModifierOperation.Multiply => currentValue * operand,
                NumericModifierOperation.Override => operand,
                _ => throw new System.NotImplementedException()
            };
        }
    }
}