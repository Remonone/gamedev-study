using System;
using System.Collections.Generic;
using Types.QTE;
using UnityEditor;
using UnityEngine;

namespace Clicker.Editor.Upgrades {
    [CustomPropertyDrawer(typeof(QteModifierEffect), true)]
    public sealed class QteModifierEffectPropertyDrawer : PropertyDrawer {
        private static List<Type> _effectTypes;
        private static GUIContent[] _effectLabels;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            if (property.propertyType != SerializedPropertyType.ManagedReference) {
                EditorGUI.HelpBox(position, "QteModifierEffect fields must be marked with [SerializeReference].", MessageType.Warning);
                return;
            }

            if (EnsureUniqueArrayReference(property)) {
                property.serializedObject.ApplyModifiedProperties();
                property.serializedObject.Update();
            }

            EnsureTypeCache();

            var lineHeight = EditorGUIUtility.singleLineHeight;
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            var headerRect = new Rect(position.x, position.y, position.width, lineHeight);
            var selectorRect = EditorGUI.PrefixLabel(headerRect, label);
            var clearRect = new Rect(selectorRect.xMax - 22f, selectorRect.y, 22f, lineHeight);
            selectorRect.width -= property.managedReferenceValue == null ? 0f : 26f;

            var currentType = property.managedReferenceValue?.GetType();
            var selectedIndex = GetSelectedIndex(currentType);

            EditorGUI.BeginChangeCheck();
            var nextIndex = EditorGUI.Popup(selectorRect, selectedIndex, _effectLabels);
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObjects(property.serializedObject.targetObjects, "Set QTE Modifier Effect");
                property.managedReferenceValue = nextIndex <= 0 ? null : Activator.CreateInstance(_effectTypes[nextIndex - 1]) as QteModifierEffect;
                property.serializedObject.ApplyModifiedProperties();
                return;
            }

            if (property.managedReferenceValue != null && GUI.Button(clearRect, "x")) {
                Undo.RecordObjects(property.serializedObject.targetObjects, "Clear QTE Modifier Effect");
                property.managedReferenceValue = null;
                property.serializedObject.ApplyModifiedProperties();
                return;
            }

            if (property.managedReferenceValue == null) return;

            DrawChildren(position, property, headerRect.yMax + spacing);
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

            var previousIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = previousIndent + 1;
            try {
                while (!SerializedProperty.EqualContents(iterator, endProperty)) {
                    var height = EditorGUI.GetPropertyHeight(iterator, true);
                    var rect = EditorGUI.IndentedRect(new Rect(position.x, y, position.width, height));
                    DrawChildProperty(rect, iterator);
                    y += height + EditorGUIUtility.standardVerticalSpacing;
                    if (!iterator.NextVisible(false)) break;
                }
            }
            finally {
                EditorGUI.indentLevel = previousIndent;
            }
        }

        private static void DrawChildProperty(Rect rect, SerializedProperty property) {
            var previousIndent = EditorGUI.indentLevel;
            var previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUI.indentLevel = 0;
            EditorGUIUtility.labelWidth = Mathf.Min(previousLabelWidth, Mathf.Max(80f, rect.width * 0.45f));
            try {
                EditorGUI.PropertyField(rect, property, true);
            }
            finally {
                EditorGUIUtility.labelWidth = previousLabelWidth;
                EditorGUI.indentLevel = previousIndent;
            }
        }

        private static void EnsureTypeCache() {
            if (_effectTypes != null) return;

            _effectTypes = new List<Type>();
            var types = TypeCache.GetTypesDerivedFrom<QteModifierEffect>();
            for (var i = 0; i < types.Count; i++) {
                var type = types[i];
                if (type.IsAbstract || type.IsGenericType || type.GetConstructor(Type.EmptyTypes) == null) continue;
                if (Attribute.GetCustomAttribute(type, typeof(SerializableAttribute)) == null) continue;
                _effectTypes.Add(type);
            }

            _effectTypes.Sort((left, right) => string.Compare(GetDisplayName(left), GetDisplayName(right), StringComparison.Ordinal));
            _effectLabels = new GUIContent[_effectTypes.Count + 1];
            _effectLabels[0] = new GUIContent("Select QTE Effect");
            for (var i = 0; i < _effectTypes.Count; i++) {
                _effectLabels[i + 1] = new GUIContent(GetDisplayName(_effectTypes[i]));
            }
        }

        private static int GetSelectedIndex(Type currentType) {
            if (currentType == null) return 0;
            for (var i = 0; i < _effectTypes.Count; i++) {
                if (_effectTypes[i] == currentType) return i + 1;
            }

            return 0;
        }

        private static string GetDisplayName(Type type) {
            var name = type.Name;
            const string suffix = "QteModifierEffect";
            if (name.EndsWith(suffix, StringComparison.Ordinal)) {
                name = name.Substring(0, name.Length - suffix.Length);
            }

            return ObjectNames.NicifyVariableName(name);
        }

        private static bool EnsureUniqueArrayReference(SerializedProperty property) {
            if (property.serializedObject.isEditingMultipleObjects
                || property.managedReferenceValue is not QteModifierEffect effect
                || !TryGetArrayElementInfo(property.propertyPath, out var arrayPath, out var index, out var suffix)
                || index <= 0) {
                return false;
            }

            var referenceId = property.managedReferenceId;
            for (var i = 0; i < index; i++) {
                var sibling = property.serializedObject.FindProperty($"{arrayPath}.Array.data[{i}]{suffix}");
                if (sibling?.propertyType != SerializedPropertyType.ManagedReference) continue;
                if (sibling.managedReferenceId != referenceId && !ReferenceEquals(sibling.managedReferenceValue, effect)) continue;

                Undo.RecordObjects(property.serializedObject.targetObjects, "Detach QTE Modifier Effect Reference");
                property.managedReferenceValue = CloneEffect(effect);
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

        private static QteModifierEffect CloneEffect(QteModifierEffect effect) {
            return CloneObject(effect, new Dictionary<object, object>(ReferenceComparer.Instance)) as QteModifierEffect;
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
