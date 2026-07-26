using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Data.Modifiers;
using Utils.Attributes;

namespace Utils.Metadata {
    public class MetadataWrapperFactory {
        private const BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static IModifiableWrapper CreateWrapper(Type entryType) {
            if (entryType == null) throw new ArgumentNullException(nameof(entryType));
            
            if (!entryType.IsValueType) throw new ArgumentException("Entry type must be a value type", nameof(entryType));
            
            var groupAttribute = entryType.GetCustomAttribute<CacheEntryGroupAttribute>();
            
            if (groupAttribute == null) throw new InvalidOperationException($"Type {entryType.FullName} does not have a CacheEntryGroupAttribute");

            var parameters = DiscoverParameters(entryType, groupAttribute.Name);
            
            var wrapperType = typeof(ModifiableWrapper<>).MakeGenericType(entryType);
            
            return (IModifiableWrapper)Activator.CreateInstance(wrapperType, groupAttribute.Name, groupAttribute.DisplayName, parameters);
        }

        private static ICacheParameterMetadata[] DiscoverParameters(Type entryType, string groupAttributeName) {
            var result = new List<ICacheParameterMetadata>();

            foreach (var property in entryType.GetProperties(MemberFlags)) {
                var attribute = property.GetCustomAttribute<ModifiableParameterAttribute>();

                if (attribute == null) continue;
                
                if(!NumericTypeUtility.IsSupportedType(property.PropertyType))
                    throw new ArgumentException($"Property {property.Name} of type {property.PropertyType} is not supported", nameof(entryType));

                var key = CacheParameterKey.Create(groupAttributeName, property.Name);
                result.Add(new PropertyParameterMetadata(entryType, key, attribute.DisplayName, property, attribute.Minimum, attribute.Maximum));
                
            }

            foreach (var field in entryType.GetFields(MemberFlags)) {
                var attribute = field.GetCustomAttribute<ModifiableParameterAttribute>();

                if (attribute == null) continue;
                
                if(!NumericTypeUtility.IsSupportedType(field.FieldType))
                    throw new ArgumentException($"Property {field.Name} of type {field.FieldType} is not supported", nameof(entryType));

                var key = CacheParameterKey.Create(groupAttributeName, field.Name);
                result.Add(new FieldParameterMetadata(entryType, key, attribute.DisplayName, field, attribute.Minimum, attribute.Maximum));
            }
            
            var duplicates = result.GroupBy(x => x.Key).Where(x => x.Count() > 1).Select(x => x.Key).ToList();

            if (duplicates.Count > 0) {
                throw new ArgumentException($"Duplicate parameter keys found: {string.Join(", ", duplicates)} for {entryType.FullName}", nameof(entryType));
            }
            return result.ToArray();
        }
    }
}