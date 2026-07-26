using System;
using System.Collections.Generic;

namespace Data.Modifiers {
    public class ModifierContext : IModifierContext {
        private readonly Dictionary<Type, object> _capabilities = new();

        public ModifierContext Add<TCapability>(TCapability capability) where TCapability : class {
            _capabilities[typeof(TCapability)] = capability ?? throw new ArgumentNullException(nameof(capability));
            return this;
        }

        public bool TryGet<TCapability>(out TCapability capability) where TCapability : class {
            if (_capabilities.TryGetValue(typeof(TCapability), out var value)) {
                capability = (TCapability)value;
                return true;
            }

            capability = null;
            return false;
        }
        
        public TCapability Require<TCapability>() where TCapability : class {
            if (TryGet(out TCapability capability)) return capability;
            throw new InvalidOperationException($"Capability '{typeof(TCapability)}' is not available in the modifier context.");
        }
    }
}