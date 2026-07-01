using System;
using System.Collections.Generic;
using FormulaSerialization.UIElements;
using Types.Modifiers.Cost.Formula;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FormulaSerialization {
    [CustomPropertyDrawer(typeof(IFormula), true)]
    public sealed class FormulaPropertyDrawer : PropertyDrawer {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            return new FormulaField(property);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            if (property.propertyType != SerializedPropertyType.ManagedReference) {
                EditorGUI.HelpBox(position, "IFormula fields must be marked with [SerializeReference].", MessageType.Warning);
                return;
            }

            if (EnsureUniqueArrayReference(property)) {
                property.serializedObject.ApplyModifiedProperties();
                property.serializedObject.Update();
            }

            var lineHeight = EditorGUIUtility.singleLineHeight;
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            var headerRect = new Rect(position.x, position.y, position.width, lineHeight);
            var selectorRect = EditorGUI.PrefixLabel(headerRect, label);
            var clearRect = new Rect(selectorRect.xMax - 22f, selectorRect.y, 22f, lineHeight);
            selectorRect.width -= property.managedReferenceValue == null ? 0f : 26f;

            var types = FormulaTypeProvider.FormulaTypes;
            var currentType = property.managedReferenceValue?.GetType();
            var selectedIndex = GetSelectedIndex(types, currentType);
            var labels = BuildOptions(types, "Select Formula");

            EditorGUI.BeginChangeCheck();
            var nextIndex = EditorGUI.Popup(selectorRect, selectedIndex, labels);
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObjects(property.serializedObject.targetObjects, "Set Formula");
                property.managedReferenceValue = nextIndex <= 0 ? null : Activator.CreateInstance(types[nextIndex - 1]) as IFormula;
                property.serializedObject.ApplyModifiedProperties();
                return;
            }

            if (property.managedReferenceValue != null && GUI.Button(clearRect, "x")) {
                Undo.RecordObjects(property.serializedObject.targetObjects, "Clear Formula");
                property.managedReferenceValue = null;
                property.serializedObject.ApplyModifiedProperties();
                return;
            }

            if (property.managedReferenceValue == null) return;

            var childY = headerRect.yMax + spacing;
            DrawChildren(position, property, childY);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            var height = EditorGUIUtility.singleLineHeight;
            if (property.propertyType != SerializedPropertyType.ManagedReference || property.managedReferenceValue == null) {
                return height;
            }

            var iterator = property.Copy();
            var endProperty = iterator.GetEndProperty();
            if (!iterator.NextVisible(true)) return height;

            while (!SerializedProperty.EqualContents(iterator, endProperty)) {
                height += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(iterator, true);
                if (!iterator.NextVisible(false)) break;
            }

            return height;
        }

        private static void DrawChildren(Rect position, SerializedProperty property, float y) {
            var iterator = property.Copy();
            var endProperty = iterator.GetEndProperty();
            if (!iterator.NextVisible(true)) return;

            var childX = position.x + EditorGUIUtility.labelWidth;
            var childWidth = position.width - EditorGUIUtility.labelWidth;
            while (!SerializedProperty.EqualContents(iterator, endProperty)) {
                var height = EditorGUI.GetPropertyHeight(iterator, true);
                var rect = new Rect(childX, y, childWidth, height);
                EditorGUI.PropertyField(rect, iterator, true);
                y += height + EditorGUIUtility.standardVerticalSpacing;
                if (!iterator.NextVisible(false)) break;
            }
        }

        private static GUIContent[] BuildOptions(IReadOnlyList<Type> types, string emptyLabel) {
            var labels = new GUIContent[types.Count + 1];
            labels[0] = new GUIContent(emptyLabel);
            for (var i = 0; i < types.Count; i++) {
                labels[i + 1] = new GUIContent(FormulaTypeProvider.GetDisplayName(types[i]));
            }

            return labels;
        }

        private static int GetSelectedIndex(IReadOnlyList<Type> types, Type currentType) {
            if (currentType == null) return 0;
            for (var i = 0; i < types.Count; i++) {
                if (types[i] == currentType) return i + 1;
            }

            return 0;
        }

        private static bool EnsureUniqueArrayReference(SerializedProperty property) {
            if (property.serializedObject.isEditingMultipleObjects
                || property.managedReferenceValue is not IFormula formula
                || !TryGetArrayElementInfo(property.propertyPath, out var arrayPath, out var index, out var suffix)
                || index <= 0) {
                return false;
            }

            var referenceId = property.managedReferenceId;
            for (var i = 0; i < index; i++) {
                var sibling = property.serializedObject.FindProperty($"{arrayPath}.Array.data[{i}]{suffix}");
                if (sibling?.propertyType != SerializedPropertyType.ManagedReference) continue;
                if (sibling.managedReferenceId != referenceId && !ReferenceEquals(sibling.managedReferenceValue, formula)) continue;

                Undo.RecordObjects(property.serializedObject.targetObjects, "Detach Formula Reference");
                property.managedReferenceValue = CloneFormula(formula);
                return true;
            }

            return false;
        }

        private static bool TryGetArrayElementInfo(string propertyPath, out string arrayPath, out int index, out string suffix) {
            const string arrayToken = ".Array.data[";
            arrayPath = null;
            index = -1;
            suffix = null;

            var tokenIndex = propertyPath.LastIndexOf(arrayToken, StringComparison.Ordinal);
            if (tokenIndex < 0) return false;

            var indexStart = tokenIndex + arrayToken.Length;
            var indexEnd = propertyPath.IndexOf(']', indexStart);
            if (indexEnd < 0 || !int.TryParse(propertyPath.Substring(indexStart, indexEnd - indexStart), out index)) return false;

            arrayPath = propertyPath.Substring(0, tokenIndex);
            suffix = propertyPath.Substring(indexEnd + 1);
            return true;
        }

        private static IFormula CloneFormula(IFormula formula) {
            return CloneObject(formula, new Dictionary<object, object>(ReferenceComparer.Instance)) as IFormula;
        }

        private static object CloneObject(object value, Dictionary<object, object> visited) {
            if (value == null) return null;

            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type.IsValueType) return value;
            if (value is UnityEngine.Object) return value;
            if (visited.TryGetValue(value, out var knownClone)) return knownClone;

            if (value is AnimationCurve curve) {
                var curveClone = new AnimationCurve(curve.keys) {
                    preWrapMode = curve.preWrapMode,
                    postWrapMode = curve.postWrapMode
                };
                visited[value] = curveClone;
                return curveClone;
            }

            if (type.IsArray) {
                var sourceArray = (Array)value;
                var elementType = type.GetElementType();
                var arrayClone = Array.CreateInstance(elementType, sourceArray.Length);
                visited[value] = arrayClone;

                for (var i = 0; i < sourceArray.Length; i++) {
                    arrayClone.SetValue(CloneObject(sourceArray.GetValue(i), visited), i);
                }

                return arrayClone;
            }

            var clone = Activator.CreateInstance(type);
            visited[value] = clone;
            foreach (var field in GetSerializedFields(type)) {
                field.SetValue(clone, CloneObject(field.GetValue(value), visited));
            }

            return clone;
        }

        private static IEnumerable<System.Reflection.FieldInfo> GetSerializedFields(Type type) {
            for (var current = type; current != null && current != typeof(object); current = current.BaseType) {
                var fields = current.GetFields(System.Reflection.BindingFlags.Instance
                                               | System.Reflection.BindingFlags.Public
                                               | System.Reflection.BindingFlags.NonPublic
                                               | System.Reflection.BindingFlags.DeclaredOnly);
                foreach (var field in fields) {
                    if (field.IsStatic || field.IsInitOnly || field.IsNotSerialized) continue;
                    if (field.IsPublic || Attribute.GetCustomAttribute(field, typeof(SerializeField)) != null) {
                        yield return field;
                    }
                }
            }
        }

        private sealed class ReferenceComparer : IEqualityComparer<object> {
            public static readonly ReferenceComparer Instance = new();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
