using System;
using UnityEngine;

namespace Types.Values {
    [Serializable]
    public struct Base {
        [Tooltip("Power of 1000 used to scale a Value.")]
        public int Degree;

        public override string ToString() {
            if (Degree <= 4) return GetDegreeConst();

            var index = Degree - 5;
            var first = index / 26;
            var second = index % 26;
            var firstLetter = (char)((first < 26 ? 'a' : 'A') + first % 26);
            var secondLetter = (char)('a' + second);
            return $"{firstLetter}{secondLetter}";
        }

        private string GetDegreeConst() {
            switch (Degree) {
                case 0: return "";
                case 1: return "k";
                case 2: return "m";
                case 3: return "b";
                case 4: return "t";
                default: return "";
            }
        }
    }
}
