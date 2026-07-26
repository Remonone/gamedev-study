using Data.Modifiers.Calculation;
using Utils;

namespace Data.Modifiers.Values {
    public struct NumericModifierValue {

        public readonly Value BaseValue;
        public Value FlatBonus;
        public double AdditivePercent;
        public double Multiplier;
        public bool HasOverride;
        public Value OverrideValue;
        
        public NumericModifierValue(Value baseValue, Value flatBonus, double additivePercent, double multiplier, bool hasOverride, Value overrideValue) {
            BaseValue = baseValue;
            FlatBonus = flatBonus;
            AdditivePercent = additivePercent;
            Multiplier = multiplier;
            HasOverride = hasOverride;
            OverrideValue = overrideValue;
        }

        public Value Result {
            get {
                if (HasOverride) {
                    return OverrideValue;
                }

                return (BaseValue + FlatBonus) * (1d + AdditivePercent) * Multiplier;
            }
        }

        public void Apply(NumericModifierOperation operation, Value operand) {
            switch(operation) {
                case NumericModifierOperation.Add: {
                    FlatBonus += operand;
                    break;
                }
                case NumericModifierOperation.AddPercent: {
                    AdditivePercent += operand.ToDouble();
                    break;
                }
                case NumericModifierOperation.Multiply: {
                    Multiplier += operand.ToDouble();
                    break;
                }
                case NumericModifierOperation.Override: {
                    OverrideValue = operand;
                    HasOverride = true;
                    break;
                }
                default:
                    throw new System.NotImplementedException();
            }
        }
    }
}