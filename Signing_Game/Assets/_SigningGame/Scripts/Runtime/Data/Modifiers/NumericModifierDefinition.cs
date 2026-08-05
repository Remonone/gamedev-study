using System;
using Data.Modifiers.Calculation;
using Data.Modifiers.Numeric;
using UnityEngine;
using Utils;
using Utils.Metadata;

namespace Data.Modifiers {
    [Serializable]
    public class NumericModifierDefinition {
        [SerializeField] private string _id;
        [SerializeField] private NumericModifierOperation _operation;
        [SerializeReference] private NumericValueDefinition _value;
        [SerializeField] private CacheParameterReference _parameter;

        public string Id => _id;

        public Value EvaluateAtLevel(int level) {
            if (level < 0) throw new ArgumentOutOfRangeException(nameof(level));
            if (level == 0) return Value.Zero;
            if (_value == null) throw new InvalidOperationException($"Numeric modifier '{_id}' has no value definition.");

            var context = new ModifierContext()
                .Add(new LevelModifierCapability(level))
                .Add(new ModifierEffectivenessCapability(1f));
            return _value.Evaluate(context);
        }
        
        public bool IsApplicable<TValue>(TValue value) {
            var wrapper = PredefinedMetadataWrapperStorage.Get(_parameter.GroupId);
            return wrapper.IsApplicable(value);
        }

        public Type GetGroupType() {
            return PredefinedMetadataWrapperStorage.Get(_parameter.GroupId).EntryType;
        }

        public TValue Apply<TValue>(TValue value, IModifierContext context) {
            var wrapper = PredefinedMetadataWrapperStorage.Get(_parameter.GroupId);
            if (!wrapper.IsApplicable(value)) return value;
            double effectiveness = context.TryGet(out ModifierEffectivenessCapability capability)
                ? capability.Effectiveness
                : 1d;
            return (TValue)wrapper.Apply(
                value,
                _parameter.ParameterId,
                _operation,
                _value.Evaluate(context).ToDouble(),
                effectiveness);
        }
        
    }
}
