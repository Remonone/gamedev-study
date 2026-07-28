using System;
using UnityEngine;
using Utils;

namespace Data.Formulas {
    [Serializable]
    public class ConstantValue : IFormula {
        
        [Tooltip("Constant value to return without any changes")]
        public Value Value;
        
        public Value Evaluate(Value input) {
            return Value;
        }
    }
}