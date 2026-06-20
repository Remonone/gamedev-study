using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Types.Modifiers.Definitions.Cost.Condition;
using UnityEditor;

namespace LevelConditionSerialization.UIElements {
    internal static class LevelConditionTypeProvider {
        private static List<Type> _conditionTypes;

        public static IReadOnlyList<Type> ConditionTypes {
            get {
                if (_conditionTypes == null) {
                    _conditionTypes = TypeCache.GetTypesDerivedFrom<ILevelCondition>()
                        .Where(IsSelectableCondition)
                        .OrderBy(type => type.Name)
                        .ThenBy(type => type.Namespace)
                        .ToList();
                }

                return _conditionTypes;
            }
        }

        public static string GetDisplayName(Type type) {
            return ObjectNames.NicifyVariableName(type.Name);
        }

        private static bool IsSelectableCondition(Type type) {
            return !type.IsAbstract
                   && !type.IsInterface
                   && type.GetCustomAttribute<SerializableAttribute>() != null
                   && type.GetConstructor(Type.EmptyTypes) != null;
        }
    }
}
