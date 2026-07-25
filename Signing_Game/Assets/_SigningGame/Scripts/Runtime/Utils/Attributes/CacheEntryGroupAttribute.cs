using System;

namespace Utils.Attributes {
    [AttributeUsage(AttributeTargets.Struct)]
    public class CacheEntryGroupAttribute : Attribute {
        public string Name { get; }
        public string DisplayName { get; }
        public CacheEntryGroupAttribute(string name, string displayName = null) {
            Name = name;
            DisplayName = string.IsNullOrEmpty(displayName) ? name : displayName;
        }
    }
}