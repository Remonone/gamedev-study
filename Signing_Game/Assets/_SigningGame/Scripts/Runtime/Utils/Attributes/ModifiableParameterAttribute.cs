using System;

namespace Utils.Attributes {
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ModifiableParameterAttribute : Attribute {
        public string Name { get; }
        public string DisplayName { get; }

        public double Minimum { get; set; } = double.NegativeInfinity;
        public double Maximum { get; set; } = double.PositiveInfinity;
        
        public ModifiableParameterAttribute(string name, string displayName = null) {
            Name = name;
            DisplayName = string.IsNullOrEmpty(displayName) ? name : displayName;
        }
    }
}