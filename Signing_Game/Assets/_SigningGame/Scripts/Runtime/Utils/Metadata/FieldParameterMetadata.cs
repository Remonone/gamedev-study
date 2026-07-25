using System;
using System.Reflection;

namespace Utils.Metadata {
    public sealed class FieldParameterMetadata : ICacheParameterMetadata {
        private readonly FieldInfo _field;

        public CacheParameterKey Key { get; }
        public string DisplayName { get; }

        public Type EntryType { get; }
        public Type ValueType => _field.FieldType;

        public double Minimum { get; }
        public double Maximum { get; }

        public FieldParameterMetadata(Type entryType, CacheParameterKey key, string displayName,
            FieldInfo field, double minimum, double maximum) {
            EntryType = entryType ?? throw new ArgumentNullException(nameof(entryType));

            Key = key;
            DisplayName = displayName;
            _field = field ?? throw new ArgumentNullException(nameof(field));

            Minimum = minimum;
            Maximum = maximum;

            if (_field.IsInitOnly) {
                throw new InvalidOperationException($"Field {_field.Name} cannot be readonly.");
            }
        }

        public object GetValue(object boxedEntry) {
            ValidateEntry(boxedEntry);

            return _field.GetValue(boxedEntry);
        }

        public void SetValue(object boxedEntry, object value) {
            ValidateEntry(boxedEntry);
            _field.SetValue(boxedEntry, value);
        }

        private void ValidateEntry(object boxedEntry) {
            if (boxedEntry == null) throw new ArgumentNullException(nameof(boxedEntry));

            if (boxedEntry.GetType() != EntryType) {
                throw new ArgumentException($"Expected entry of type {EntryType.FullName}, " +
                                            $"but received {boxedEntry.GetType().FullName}.");
            }
        }
    }
}