using System.Linq;

namespace Types.Enums.Cost {
    public struct Price {
        public Entry[] Entries;

        public Price(params Entry[] entries) {
            Entries = entries;
        }

        public override string ToString() {
            if (Entries == null || Entries.Length == 0) return "0";
            return string.Join(" + ", Entries.Select(entry => $"{entry.Price} {entry.StructureType}"));
        }

        public struct Entry {
            public StructureType StructureType;
            public decimal Price;

            public Entry(StructureType type, decimal price) {
                StructureType = type;
                Price = price;
            }
        }
    }
}
