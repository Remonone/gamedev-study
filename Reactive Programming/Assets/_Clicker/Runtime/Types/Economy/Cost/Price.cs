using System.Linq;
using Types.Enums;
using Types.Values;

namespace Types.Modifiers.Cost {
    public struct Price {
        public Entry[] Entries;

        public Price(params Entry[] entries) {
            Entries = entries;
        }

        public override string ToString() {
            if (Entries == null || Entries.Length == 0) return "0";
            return string.Join(" + ", Entries.Select(entry => $"{entry.Price} {entry.GovernmentInteractionType}"));
        }

        public struct Entry {
            public GovernmentInteractionType GovernmentInteractionType;
            public Value Price;

            public Entry(GovernmentInteractionType type, Value price) {
                GovernmentInteractionType = type;
                Price = price;
            }
        }
    }
}
