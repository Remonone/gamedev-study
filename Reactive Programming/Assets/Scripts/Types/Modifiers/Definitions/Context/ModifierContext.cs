using System;
using System.Collections.Generic;

namespace Types.Modifiers.Context {
    
    public sealed class ModifierContext : IModifierContext {
        private Dictionary<Type, object> _capabilities { get; } = new();
        
        public void Add<TCapability>(TCapability capability) where TCapability : class {
            _capabilities[typeof(TCapability)] = capability;
        }
        
        public bool TryGet<TCapability>(out TCapability capability) where TCapability : class {
            if (_capabilities.TryGetValue(typeof(TCapability), out var value)) {
                capability = (TCapability)value;
                return true;
            }    
            capability = null;
            return false;
        }
    }
}