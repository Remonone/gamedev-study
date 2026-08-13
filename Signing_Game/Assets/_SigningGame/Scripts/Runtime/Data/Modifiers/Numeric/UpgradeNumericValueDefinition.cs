using System;
using Data.Formulas;
using UnityEngine;
using Utils;

namespace Data.Modifiers.Numeric {
    [Serializable]
    public sealed class UpgradeNumericValueDefinition : NumericValueDefinition {
        [SerializeField] private Value _baseValue;
        [SerializeReference] private IFormula _formula;

        public override bool IncludesEffectiveness => true;

        public override Value Evaluate(IModifierContext context) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (_formula == null) {
                throw new InvalidOperationException("An upgrade numeric value requires a formula.");
            }

            int level = context.Require<LevelModifierCapability>().Level;
            float effectiveness = context.Require<ModifierEffectivenessCapability>().Effectiveness;
            return _baseValue * _formula.Evaluate(new Value(level)) * effectiveness;
        }
    }
}
