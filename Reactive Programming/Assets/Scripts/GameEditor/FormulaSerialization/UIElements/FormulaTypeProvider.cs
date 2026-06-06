using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Types.Economy.Cost.Formula;
using UnityEditor;

namespace GameEditor.FormulaSerialization.UIElements {
    internal static class FormulaTypeProvider {
        private static List<Type> _formulaTypes;

        public static IReadOnlyList<Type> FormulaTypes {
            get {
                if (_formulaTypes == null) {
                    _formulaTypes = TypeCache.GetTypesDerivedFrom<IFormula>()
                        .Where(IsSelectableFormula)
                        .OrderBy(type => type.Name)
                        .ThenBy(type => type.Namespace)
                        .ToList();
                }

                return _formulaTypes;
            }
        }

        public static string GetDisplayName(Type type) {
            return ObjectNames.NicifyVariableName(type.Name);
        }

        private static bool IsSelectableFormula(Type type) {
            return !type.IsAbstract
                   && !type.IsInterface
                   && type.GetCustomAttribute<SerializableAttribute>() != null
                   && type.GetConstructor(Type.EmptyTypes) != null;
        }
    }
}
