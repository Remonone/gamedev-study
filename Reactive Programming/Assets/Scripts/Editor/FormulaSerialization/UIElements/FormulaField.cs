using System;
using Types.Economy.Cost.Formula;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FormulaSerialization.UIElements {
    public sealed class FormulaField : VisualElement {
        private readonly SerializedProperty _property;
        private readonly ToolbarMenu _selector;
        private readonly Button _clearButton;
        private readonly VisualElement _fieldsContainer;

        public FormulaField(SerializedProperty property) {
            _property = property.Copy();

            AddToClassList("formula-field");
            style.flexDirection = FlexDirection.Column;
            style.marginTop = 2;
            style.marginBottom = 2;

            if (_property.propertyType != SerializedPropertyType.ManagedReference) {
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

            var label = new Label(_property.displayName) {
                style = {
                    minWidth = EditorGUIUtility.labelWidth - 15,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };

            _selector = new ToolbarMenu {
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
            _property.serializedObject.Update();
            _fieldsContainer.Clear();
            RebuildSelector();

            var hasFormula = _property.managedReferenceValue != null;
            _clearButton.style.display = hasFormula ? DisplayStyle.Flex : DisplayStyle.None;
            _clearButton.SetEnabled(hasFormula);

            if (hasFormula) {
                AddFormulaFields();
            }
        }

        private void RebuildSelector() {
            _selector.menu.ClearItems();

            var currentType = _property.managedReferenceValue?.GetType();
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

        private void AddFormulaFields() {
            var iterator = _property.Copy();
            var endProperty = iterator.GetEndProperty();

            if (!iterator.NextVisible(true)) {
                return;
            }

            while (!SerializedProperty.EqualContents(iterator, endProperty)) {
                _fieldsContainer.Add(new PropertyField(iterator.Copy()));

                if (!iterator.NextVisible(false)) {
                    break;
                }
            }
        }

        private void SetFormula(Type formulaType) {
            Undo.RecordObjects(_property.serializedObject.targetObjects, "Set Formula");
            _property.serializedObject.Update();
            _property.managedReferenceValue = Activator.CreateInstance(formulaType) as IFormula;
            _property.serializedObject.ApplyModifiedProperties();
            Rebuild();
        }

        private void ClearFormula() {
            Undo.RecordObjects(_property.serializedObject.targetObjects, "Clear Formula");
            _property.serializedObject.Update();
            _property.managedReferenceValue = null;
            _property.serializedObject.ApplyModifiedProperties();
            Rebuild();
        }
    }
}
