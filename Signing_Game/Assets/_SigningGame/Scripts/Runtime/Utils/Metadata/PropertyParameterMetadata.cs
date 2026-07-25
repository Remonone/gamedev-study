using System;
using System.Reflection;

namespace Utils.Metadata {
    public sealed class PropertyParameterMetadata : ICacheParameterMetadata {
        private readonly PropertyInfo _property;

        public CacheParameterKey Key { get; }
        public string DisplayName { get; }

        public Type EntryType { get; }
        public Type ValueType => _property.PropertyType;

        public double Minimum { get; }
        public double Maximum { get; }

        public PropertyParameterMetadata(Type entryType, CacheParameterKey key, string displayName, PropertyInfo property, double minimum, double maximum) {
            EntryType = entryType ?? throw new ArgumentNullException(nameof(entryType));

            Key = key;
            DisplayName = displayName;
            _property = property ?? throw new ArgumentNullException(nameof(property));

            Minimum = minimum;
            Maximum = maximum;

            if (!_property.CanRead) {
                throw new InvalidOperationException($"Property {_property.Name} must be readable.");
            }

            if (!_property.CanWrite) {
                throw new InvalidOperationException($"Property {_property.Name} must be writable.");
            }
        }

        public object GetValue(object boxedEntry) {
            ValidateEntry(boxedEntry);

            return _property.GetValue(boxedEntry);
        }

        public void SetValue(object boxedEntry, object value) {
            ValidateEntry(boxedEntry);
            _property.SetValue(boxedEntry, value);
        }

        private void ValidateEntry(object boxedEntry) {
            if (boxedEntry == null) throw new ArgumentNullException(nameof(boxedEntry));

            if (boxedEntry.GetType() != EntryType) {
                throw new ArgumentException($"Expected entry of type {EntryType.FullName}, " +
                                            $"but received {boxedEntry.GetType().FullName}."
                );
            }
        }
    }
}