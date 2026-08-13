using System;
using Utils;

namespace Data.Modifiers.Numeric {
    [Serializable]
    public abstract class NumericValueDefinition {
        public virtual bool IncludesEffectiveness => false;

        public abstract Value Evaluate(IModifierContext context);
    }
}
