using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Types.Modifiers.Cost.Formula;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FormulaSerialization.UIElements {
    public sealed class FormulaField : VisualElement {
        private readonly SerializedObject _serializedObject;
        private readonly string _propertyPath;
        private readonly ToolbarMenu _selector;
        private readonly Button _clearButton;
        private readonly VisualElement _fieldsContainer;

        public FormulaField(SerializedProperty property) {
            _serializedObject = property.serializedObject;
            _propertyPath = property.propertyPath;

            AddToClassList("formula-field");
            style.flexDirection = FlexDirection.Column;
            style.marginTop = 2;
            style.marginBottom = 2;

            if (property.propertyType != SerializedPropertyType.ManagedReference) {
                Add(new HelpBox("IFormula fields must be marked with [SerializeReference].", HelpBoxMessageType.Warning));
                return;
            }

            var header = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 2
                }
            };

            var label = new Label(property.displayName) {
                tooltip = property.tooltip,
                style = {
                    minWidth = EditorGUIUtility.labelWidth - 15,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };

            _selector = new ToolbarMenu {
                tooltip = property.tooltip,
                style = {
                    flexGrow = 1,
                    minWidth = 120
                }
            };

            _clearButton = new Button(ClearFormula) {
                text = "x",
                tooltip = "Clear formula",
                style = {
                    width = 22,
                    marginLeft = 3,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };

            header.Add(label);
            header.Add(_selector);
            header.Add(_clearButton);

            _fieldsContainer = new VisualElement {
                style = {
                    marginLeft = EditorGUIUtility.labelWidth - 15
                }
            };

            Add(header);
            Add(_fieldsContainer);
            Rebuild();
        }

        private void Rebuild() {
            _serializedObject.Update();
            var property = FindProperty();
            if (property == null) {
                _fieldsContainer.Clear();
                _selector.SetEnabled(false);
                _clearButton.SetEnabled(false);
                return;
            }

            if (EnsureUniqueArrayReference(property)) {
                _serializedObject.ApplyModifiedProperties();
                _serializedObject.Update();
                property = FindProperty();
            }

            _fieldsContainer.Clear();
            RebuildSelector(property);

            var hasFormula = property.managedReferenceValue != null;
            _clearButton.style.display = hasFormula ? DisplayStyle.Flex : DisplayStyle.None;
            _clearButton.SetEnabled(hasFormula);

            if (hasFormula) {
                AddFormulaFields(property);
            }
        }

        private void RebuildSelector(SerializedProperty property) {
            _selector.menu.ClearItems();

            var currentType = property.managedReferenceValue?.GetType();
            _selector.text = currentType == null
                ? "Select Formula"
                : FormulaTypeProvider.GetDisplayName(currentType);

            var formulaTypes = FormulaTypeProvider.FormulaTypes;
            if (formulaTypes.Count == 0) {
                _selector.menu.AppendAction(
                    "No serializable formulas found",
                    _ => { },
                    _ => DropdownMenuAction.Status.Disabled);
                return;
            }

            foreach (var formulaType in formulaTypes) {
                var capturedType = formulaType;
                _selector.menu.AppendAction(
                    FormulaTypeProvider.GetDisplayName(capturedType),
                    _ => SetFormula(capturedType),
                    _ => capturedType == currentType
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal);
            }
        }

        private void AddFormulaFields(SerializedProperty property) {
            var iterator = property.Copy();
            var endProperty = iterator.GetEndProperty();

            if (!iterator.NextVisible(true)) {
                return;
            }

            while (!SerializedProperty.EqualContents(iterator, endProperty)) {
                var childProperty = iterator.Copy();
                var childField = new PropertyField(childProperty);
                childField.BindProperty(childProperty);
                _fieldsContainer.Add(childField);

                if (!iterator.NextVisible(false)) {
                    break;
                }
            }
        }

        private void SetFormula(Type formulaType) {
            var property = FindProperty();
            if (property == null) {
                return;
            }

            Undo.RecordObjects(_serializedObject.targetObjects, "Set Formula");
            _serializedObject.Update();
            property = FindProperty();
            property.managedReferenceValue = Activator.CreateInstance(formulaType) as IFormula;
            _serializedObject.ApplyModifiedProperties();
            Rebuild();
        }

        private void ClearFormula() {
            var property = FindProperty();
            if (property == null) {
                return;
            }

            Undo.RecordObjects(_serializedObject.targetObjects, "Clear Formula");
            _serializedObject.Update();
            property = FindProperty();
            property.managedReferenceValue = null;
            _serializedObject.ApplyModifiedProperties();
            Rebuild();
        }

        private SerializedProperty FindProperty() {
            return _serializedObject.FindProperty(_propertyPath);
        }

        private bool EnsureUniqueArrayReference(SerializedProperty property) {
            if (_serializedObject.isEditingMultipleObjects
                || property.managedReferenceValue is not IFormula formula
                || !TryGetArrayElementInfo(property.propertyPath, out var arrayPath, out var index, out var suffix)
                || index <= 0) {
                return false;
            }

            var referenceId = property.managedReferenceId;
            for (var i = 0; i < index; i++) {
                var sibling = _serializedObject.FindProperty($"{arrayPath}.Array.data[{i}]{suffix}");
                if (sibling?.propertyType != SerializedPropertyType.ManagedReference) {
                    continue;
                }

                if (sibling.managedReferenceId != referenceId && !ReferenceEquals(sibling.managedReferenceValue, formula)) {
                    continue;
                }

                Undo.RecordObjects(_serializedObject.targetObjects, "Detach Formula Reference");
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
            if (tokenIndex < 0) {
                return false;
            }

            var indexStart = tokenIndex + arrayToken.Length;
            var indexEnd = propertyPath.IndexOf(']', indexStart);
            if (indexEnd < 0 || !int.TryParse(propertyPath.Substring(indexStart, indexEnd - indexStart), out index)) {
                return false;
            }

            arrayPath = propertyPath.Substring(0, tokenIndex);
            suffix = propertyPath.Substring(indexEnd + 1);
            return true;
        }

        private static IFormula CloneFormula(IFormula formula) {
            return CloneObject(formula, new Dictionary<object, object>(ReferenceComparer.Instance)) as IFormula;
        }

        private static object CloneObject(object value, Dictionary<object, object> visited) {
            if (value == null) {
                return null;
            }

            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type.IsValueType) {
                return value;
            }

            if (value is UnityEngine.Object) {
                return value;
            }

            if (visited.TryGetValue(value, out var knownClone)) {
                return knownClone;
            }

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

        private static IEnumerable<FieldInfo> GetSerializedFields(Type type) {
            for (var current = type; current != null && current != typeof(object); current = current.BaseType) {
                var fields = current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                foreach (var field in fields) {
                    if (field.IsStatic || field.IsInitOnly || field.IsNotSerialized) {
                        continue;
                    }

                    if (field.IsPublic || field.GetCustomAttribute<SerializeField>() != null) {
                        yield return field;
                    }
                }
            }
        }

        private sealed class ReferenceComparer : IEqualityComparer<object> {
            public static readonly ReferenceComparer Instance = new();

            public new bool Equals(object x, object y) {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj) {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
