using System;
using UnityEngine;

namespace Utils.Properties {
    [Serializable]
    public class FloatProperty : Property {
        
        public float Value;
        
        public override float GetValue(int level) {
            return Operation switch {
                PropertyOperation.Multiplication => Value * Argument * level,
                PropertyOperation.Addition => Value + Argument * level,
                _ => Value,
            };
        }
    }
}