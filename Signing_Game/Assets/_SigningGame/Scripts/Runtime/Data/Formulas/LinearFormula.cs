using System;
using UnityEngine;
using Utils;

namespace Data.Formulas {
    [Serializable]
    public class LinearFormula : IFormula {

        [Tooltip("Starting value added before input scaling")]
        public Value BaseValue;
        [Tooltip("Amount added for each input unit")]
        public Value Slope;
        
        public Value Evaluate(Value input) {
            return BaseValue + input * Slope;
        }
    }
}