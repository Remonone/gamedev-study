using System;
using Data.Modifiers.Calculation;
using Data.Modifiers.Numeric;
using UnityEngine;
using Utils.Metadata;

namespace Data.Modifiers {
    [Serializable]
    public class NumericModifierDefinition {
        [SerializeField] private NumericModifierOperation _operation;
        [SerializeReference] private NumericValueDefinition _value;
        [SerializeField] private CacheParameterReference _parameter;
        
        public bool IsApplicable<TValue>(TValue value) {
            var wrapper = PredefinedMetadataWrapperStorage.Get(_parameter.GroupId);
            return wrapper.IsApplicable(value);
        }

        public TValue Apply<TValue>(TValue value, IModifierContext context) {
            var wrapper = PredefinedMetadataWrapperStorage.Get(_parameter.GroupId);
            if (!wrapper.IsApplicable(value)) return value;
            return (TValue)wrapper.Apply(value, _parameter.ParameterId, _operation, _value.Evaluate(context).ToDouble());
        }
        
    }
}