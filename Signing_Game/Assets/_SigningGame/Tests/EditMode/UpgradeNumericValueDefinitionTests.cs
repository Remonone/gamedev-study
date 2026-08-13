using System.Reflection;
using Data.Cache;
using Data.Formulas;
using Data.Modifiers;
using Data.Modifiers.Calculation;
using Data.Modifiers.Numeric;
using NUnit.Framework;
using Utils;
using Utils.Metadata;

namespace Tests.EditMode {
    public class UpgradeNumericValueDefinitionTests {
        [Test]
        public void Evaluate_MultipliesBaseValueByLevelFormulaAndEffectiveness() {
            var definition = CreateDefinition(
                new Value(2d),
                new LinearFormula { BaseValue = Value.One, Slope = new Value(2d) });
            var context = new ModifierContext()
                .Add(new LevelModifierCapability(3))
                .Add(new ModifierEffectivenessCapability(0.5f));

            Value result = definition.Evaluate(context);

            Assert.That(result.ToDouble(), Is.EqualTo(7d).Within(0.0001d));
            Assert.That(definition.IncludesEffectiveness, Is.True);
        }

        [Test]
        public void Apply_DoesNotApplyEffectivenessTwice() {
            PredefinedMetadataWrapperStorage.Rebuild();
            UpgradeNumericValueDefinition value = CreateDefinition(
                new Value(2d),
                new LinearFormula { BaseValue = Value.One, Slope = new Value(2d) });
            NumericModifierDefinition modifier = CreateModifier(value);
            var context = new ModifierContext()
                .Add(new LevelModifierCapability(3))
                .Add(new ModifierEffectivenessCapability(0.5f));

            GenerationEntries result = modifier.Apply(new GenerationEntries(10f, 0f), context);

            Assert.That(result.TokenPerSecond, Is.EqualTo(17f).Within(0.0001f));
        }

        private static UpgradeNumericValueDefinition CreateDefinition(Value baseValue, IFormula formula) {
            var definition = new UpgradeNumericValueDefinition();
            typeof(UpgradeNumericValueDefinition)
                .GetField("_baseValue", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(definition, baseValue);
            typeof(UpgradeNumericValueDefinition)
                .GetField("_formula", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(definition, formula);
            return definition;
        }

        private static NumericModifierDefinition CreateModifier(NumericValueDefinition value) {
            var parameter = new CacheParameterReference();
            SetPrivate(parameter, "_groupId", "Generation");
            SetPrivate(parameter, "_parameterId", nameof(GenerationEntries.TokenPerSecond));

            var modifier = new NumericModifierDefinition();
            SetPrivate(modifier, "_id", "upgrade_generation_add");
            SetPrivate(modifier, "_operation", NumericModifierOperation.Add);
            SetPrivate(modifier, "_value", value);
            SetPrivate(modifier, "_parameter", parameter);
            return modifier;
        }

        private static void SetPrivate(object target, string fieldName, object value) {
            target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }
    }
}
