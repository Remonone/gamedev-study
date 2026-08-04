namespace Data.Modifiers.Calculation {
    public static class NumericModifierCalculator {
        public static double Apply(
            double currentValue,
            NumericModifierOperation operation,
            double operand,
            double effectiveness = 1d) {
            if (double.IsNaN(effectiveness) || effectiveness <= 0d) return currentValue;
            effectiveness = System.Math.Min(effectiveness, 1d);

            double fullResult = operation switch {
                NumericModifierOperation.Add => currentValue + operand,
                NumericModifierOperation.AddPercent => currentValue + (1d * operand),
                NumericModifierOperation.Multiply => currentValue * operand,
                NumericModifierOperation.Override => operand,
                _ => throw new System.NotImplementedException()
            };
            if (double.IsNaN(fullResult)) return currentValue;
            if (effectiveness >= 1d) return fullResult;
            return currentValue + (fullResult - currentValue) * effectiveness;
        }
    }
}
