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
        public NumericModifierOperation Operation => _operation;
        public string ParameterGroupId => _parameter?.GroupId;
        public string ParameterId => _parameter?.ParameterId;

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

        internal void ValidateConfiguration() {
            if (string.IsNullOrWhiteSpace(_id)) {
                throw new InvalidOperationException("A numeric modifier requires a non-empty ID.");
            }
            if (_value == null) {
                throw new InvalidOperationException($"Numeric modifier '{_id}' has no value definition.");
            }
            if (!Enum.IsDefined(typeof(NumericModifierOperation), _operation)) {
                throw new InvalidOperationException($"Numeric modifier '{_id}' has an unsupported operation value.");
            }
            if (_parameter == null || string.IsNullOrWhiteSpace(_parameter.GroupId) ||
                string.IsNullOrWhiteSpace(_parameter.ParameterId)) {
                throw new InvalidOperationException($"Numeric modifier '{_id}' has no target parameter.");
            }
            IModifiableWrapper wrapper = PredefinedMetadataWrapperStorage.Get(_parameter.GroupId);
            if (!wrapper.TryGetParameter(_parameter.ParameterId, out _)) {
                throw new InvalidOperationException(
                    $"Numeric modifier '{_id}' targets unknown parameter '{_parameter.ParameterId}' " +
                    $"in group '{_parameter.GroupId}'.");
            }
        }

        public TValue Apply<TValue>(
            TValue value,
            IModifierContext context,
            bool allowOverdrive = false) {
            var wrapper = PredefinedMetadataWrapperStorage.Get(_parameter.GroupId);
            if (!wrapper.IsApplicable(value)) return value;
            double effectiveness = _value.IncludesEffectiveness
                ? 1d
                : context.TryGet(out ModifierEffectivenessCapability capability)
                    ? capability.Effectiveness
                    : 1d;
            return (TValue)wrapper.Apply(
                value,
                _parameter.ParameterId,
                _operation,
                _value.Evaluate(context).ToDouble(),
                effectiveness,
                allowOverdrive);
        }
        
    }
}
