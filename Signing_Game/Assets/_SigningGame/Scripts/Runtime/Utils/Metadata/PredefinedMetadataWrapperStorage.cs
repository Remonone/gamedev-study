using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Data.Modifiers;
using UnityEditor;
using UnityEngine;
using Utils.Attributes;

namespace Utils.Metadata {
    public class PredefinedMetadataWrapperStorage {
        private static readonly Dictionary<string, IModifiableWrapper> WrappersById = new(StringComparer.Ordinal);

        private static readonly List<IModifiableWrapper> WrapperList = new();

        private static bool _initialized;

        public static IReadOnlyList<IModifiableWrapper> Wrappers {
            get {
                EnsureInitialized();
                return WrapperList;
            }
        }

        public static bool TryGet(
            string groupId,
            out IModifiableWrapper wrapper
        ) {
            EnsureInitialized();

            return WrappersById.TryGetValue(
                groupId,
                out wrapper
            );
        }

        public static IModifiableWrapper Get(string groupId)
        {
            EnsureInitialized();

            if (WrappersById.TryGetValue(groupId, out var wrapper))
            {
                return wrapper;
            }

            throw new KeyNotFoundException(
                $"Cache wrapper group '{groupId}' is not registered."
            );
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void InitializeRuntime() {
            Rebuild();
        }

    #if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void InitializeEditor() {
            Rebuild();
        }
    #endif

        public static void Rebuild() {
            WrappersById.Clear();
            WrapperList.Clear();

            foreach (var entryType in GetCacheEntryTypes()) {
                Register(entryType);
            }

            _initialized = true;
        }

        private static void EnsureInitialized() {
            if (_initialized) return;

            Rebuild();
        }

        private static void Register(Type entryType) {
            var groupAttribute =
                entryType.GetCustomAttribute<CacheEntryGroupAttribute>();

            if (groupAttribute == null)
                return;

            ValidateEntryType(entryType, groupAttribute);

            var wrapper = MetadataWrapperFactory.CreateWrapper(entryType);

            if (!WrappersById.TryAdd(groupAttribute.Name, wrapper)) {
                var existing =
                    WrappersById[groupAttribute.Name];

                throw new InvalidOperationException(
                    $"Duplicate cache-entry group ID " +
                    $"'{groupAttribute.Name}'. Types: " +
                    $"'{entryType.FullName}'."
                );
            }

            WrapperList.Add(wrapper);
        }

        private static void ValidateEntryType(Type entryType, CacheEntryGroupAttribute attribute) {
            if (string.IsNullOrWhiteSpace(attribute.Name)) {
                throw new InvalidOperationException(
                    $"Cache-entry type '{entryType.FullName}' " +
                    $"has an empty group ID."
                );
            }

            if (!entryType.IsValueType || entryType.IsEnum) {
                throw new InvalidOperationException(
                    $"Cache-entry group '{attribute.Name}' must be " +
                    $"declared as a struct. Actual type: " +
                    $"'{entryType.FullName}'."
                );
            }
        }

        private static IEnumerable<Type> GetCacheEntryTypes() {
    #if UNITY_EDITOR
            return TypeCache
                .GetTypesWithAttribute<CacheEntryGroupAttribute>()
                .Where(type => type != null);
    #else
            return GetRuntimeCacheEntryTypes();
    #endif
        }

        private static IEnumerable<Type> GetRuntimeCacheEntryTypes() {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                foreach (var type in GetLoadableTypes(assembly)) {
                    if (type.GetCustomAttribute<
                            CacheEntryGroupAttribute
                        >() != null)
                    {
                        yield return type;
                    }
                }
            }
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly) {
            try {
                return assembly.GetTypes();
            }catch (ReflectionTypeLoadException exception) {
                return exception.Types
                    .Where(type => type != null);
            }
        }
    }
}