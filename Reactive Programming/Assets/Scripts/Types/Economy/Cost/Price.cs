using System.Linq;

namespace Types.Enums.Cost {
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
            public decimal Price;

            public Entry(GovernmentInteractionType type, decimal price) {
                GovernmentInteractionType = type;
                Price = price;
            }
        }
    }
}
