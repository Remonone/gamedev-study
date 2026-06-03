using System;

namespace Utils.Properties {
    [Serializable]
    public class IntProperty : Property {

        public int Value;
        
        public override float GetValue(int level) {
            return Operation switch {
                PropertyOperation.Multiplication => Value * Argument * level,
                PropertyOperation.Addition => Value + Argument * level,
                _ => Value,
            };
        }
    }
}