using System;
using UnityEngine;

namespace Utils.Properties {
    [Serializable]
    public abstract class Property {
        public enum PropertyOperation {
            Multiplication,
            Addition,
            None
        }
        [Tooltip("Operation to apply to the value")]
        public PropertyOperation Operation;
        [Tooltip("Argument for the operation with level")]
        public float Argument;

        public abstract float GetValue(int level);
    }
}