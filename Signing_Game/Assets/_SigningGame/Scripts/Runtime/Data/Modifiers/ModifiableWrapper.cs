using System;
using System.Collections.Generic;
using Data.Modifiers.Calculation;
using Utils.Metadata;

namespace Data.Modifiers {
    public class ModifiableWrapper<T> : IModifiableWrapper where T : struct {
        
        private readonly Dictionary<string, ICacheParameterMetadata> _parameters;
        
        public string GroupId { get; }
        public string DisplayName { get; }
        
        public Type EntryType => typeof(T);
        public IReadOnlyCollection<ICacheParameterMetadata> Parameters => _parameters.Values;

        public ModifiableWrapper(string groupId, string displayName, IEnumerable<ICacheParameterMetadata> parameters) {
            GroupId = groupId;
            DisplayName = displayName;
            _parameters = new(StringComparer.Ordinal);
            foreach (var parameter in parameters) {
                if (parameter.EntryType != typeof(T))
                    throw new ArgumentException(
                        $"Parameter type {parameter.EntryType} does not match wrapper type {typeof(T)}"
                    );
                if (!_parameters.TryAdd(parameter.Key.ParameterId, parameter))
                    throw new ArgumentException($"Parameter {parameter.Key.ParameterId} is already registered inside {groupId}.");
            }
        }

        public bool TryGetParameter(string parameterId, out ICacheParameterMetadata parameter) {
            return _parameters.TryGetValue(parameterId, out parameter);
        }

        public bool IsApplicable(object source) {
            return source is T;
        }

        public T Apply(
            in T source,
            string parameterId,
            NumericModifierOperation operation,
            double operand,
            double effectiveness = 1d,
            bool allowOverdrive = false) {
            if (!TryGetParameter(parameterId, out var parameter))
                throw new KeyNotFoundException($"Parameter {parameterId} is not registered inside {GroupId}.");
            object boxed = source;
            
            var rawValue = parameter.GetValue(boxed);
            // BUG: If Value-instance is applied and it is greater than 2^64 then it will be clamped to double.MaxValue 
            var currentValue = NumericTypeUtility.ToDouble(rawValue);
            if (double.IsNaN(currentValue) || double.IsInfinity(currentValue)) return source;
            var modifiedValue = NumericModifierCalculator.Apply(
                currentValue,
                operation,
                operand,
                effectiveness,
                allowOverdrive);

            NumericTypeUtility.GetFiniteRange(parameter.ValueType, out double typeMinimum, out double typeMaximum);
            double minimum = Math.Max(parameter.Minimum, typeMinimum);
            double maximum = Math.Min(parameter.Maximum, typeMaximum);
            if (minimum > maximum) {
                throw new InvalidOperationException(
                    $"Parameter '{parameter.Key}' has no values inside the supported range of {parameter.ValueType}.");
            }

            if (double.IsNaN(modifiedValue)) modifiedValue = currentValue;
            else if (double.IsPositiveInfinity(modifiedValue)) modifiedValue = maximum;
            else if (double.IsNegativeInfinity(modifiedValue)) modifiedValue = minimum;

            modifiedValue = Math.Clamp(modifiedValue, minimum, maximum);
            
            parameter.SetValue(boxed, NumericTypeUtility.FromDouble(modifiedValue, parameter.ValueType));
            return (T)boxed;
        }
        
        object IModifiableWrapper.Apply(
            object source,
            string parameterId,
            NumericModifierOperation operation,
            double operand,
            double effectiveness,
            bool allowOverdrive) {
            if (source is not T entry) {
                throw new ArgumentException($"Source {source} is not of type {typeof(T)}");
            }
            return Apply(entry, parameterId, operation, operand, effectiveness, allowOverdrive);
        }
    }
}
