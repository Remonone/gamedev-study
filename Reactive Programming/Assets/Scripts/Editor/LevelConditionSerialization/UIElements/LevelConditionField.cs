using System;
using Types.Enums.Cost.Condition;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelConditionSerialization.UIElements {
    public sealed class LevelConditionField : VisualElement {
        private readonly SerializedProperty _property;
        private readonly ToolbarMenu _selector;
        private readonly Button _clearButton;
        private readonly VisualElement _fieldsContainer;

        public LevelConditionField(SerializedProperty property) {
            _property = property.Copy();

            AddToClassList("level-condition-field");
            style.flexDirection = FlexDirection.Column;
            style.marginTop = 2;
            style.marginBottom = 2;

            if (_property.propertyType != SerializedPropertyType.ManagedReference) {
                Add(new HelpBox("ILevelCondition fields must be marked with [SerializeReference].", HelpBoxMessageType.Warning));
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
                tooltip = _property.tooltip,
                style = {
                    minWidth = EditorGUIUtility.labelWidth - 15,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };

            _selector = new ToolbarMenu {
                tooltip = _property.tooltip,
                style = {
                    flexGrow = 1,
                    minWidth = 120
                }
            };

            _clearButton = new Button(ClearCondition) {
                text = "x",
                tooltip = "Clear condition",
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

            var hasCondition = _property.managedReferenceValue != null;
            _clearButton.style.display = hasCondition ? DisplayStyle.Flex : DisplayStyle.None;
            _clearButton.SetEnabled(hasCondition);

            if (hasCondition) {
                AddConditionFields();
            }
        }

        private void RebuildSelector() {
            _selector.menu.ClearItems();

            var currentType = _property.managedReferenceValue?.GetType();
            _selector.text = currentType == null
                ? "Select Condition"
                : LevelConditionTypeProvider.GetDisplayName(currentType);

            var conditionTypes = LevelConditionTypeProvider.ConditionTypes;
            if (conditionTypes.Count == 0) {
                _selector.menu.AppendAction(
                    "No serializable level conditions found",
                    _ => { },
                    _ => DropdownMenuAction.Status.Disabled);
                return;
            }

            foreach (var conditionType in conditionTypes) {
                var capturedType = conditionType;
                _selector.menu.AppendAction(
                    LevelConditionTypeProvider.GetDisplayName(capturedType),
                    _ => SetCondition(capturedType),
                    _ => capturedType == currentType
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal);
            }
        }

        private void AddConditionFields() {
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

        private void SetCondition(Type conditionType) {
            Undo.RecordObjects(_property.serializedObject.targetObjects, "Set Level Condition");
            _property.serializedObject.Update();
            _property.managedReferenceValue = Activator.CreateInstance(conditionType) as ILevelCondition;
            _property.serializedObject.ApplyModifiedProperties();
            Rebuild();
        }

        private void ClearCondition() {
            Undo.RecordObjects(_property.serializedObject.targetObjects, "Clear Level Condition");
            _property.serializedObject.Update();
            _property.managedReferenceValue = null;
            _property.serializedObject.ApplyModifiedProperties();
            Rebuild();
        }
    }
}
