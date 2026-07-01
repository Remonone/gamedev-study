using System;
using System.Collections.Generic;
using LevelConditionSerialization.UIElements;
using Types.Modifiers.Cost.Condition;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelConditionSerialization {
    [CustomPropertyDrawer(typeof(ILevelCondition), true)]
    public sealed class LevelConditionPropertyDrawer : PropertyDrawer {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            return new LevelConditionField(property);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            if (property.propertyType != SerializedPropertyType.ManagedReference) {
                EditorGUI.HelpBox(position, "ILevelCondition fields must be marked with [SerializeReference].", MessageType.Warning);
                return;
            }

            var lineHeight = EditorGUIUtility.singleLineHeight;
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            var headerRect = new Rect(position.x, position.y, position.width, lineHeight);
            var selectorRect = EditorGUI.PrefixLabel(headerRect, label);
            var clearRect = new Rect(selectorRect.xMax - 22f, selectorRect.y, 22f, lineHeight);
            selectorRect.width -= property.managedReferenceValue == null ? 0f : 26f;

            var types = LevelConditionTypeProvider.ConditionTypes;
            var currentType = property.managedReferenceValue?.GetType();
            var selectedIndex = GetSelectedIndex(types, currentType);

            EditorGUI.BeginChangeCheck();
            var nextIndex = EditorGUI.Popup(selectorRect, selectedIndex, BuildOptions(types));
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObjects(property.serializedObject.targetObjects, "Set Level Condition");
                property.managedReferenceValue = nextIndex <= 0 ? null : Activator.CreateInstance(types[nextIndex - 1]) as ILevelCondition;
                property.serializedObject.ApplyModifiedProperties();
                return;
            }

            if (property.managedReferenceValue != null && GUI.Button(clearRect, "x")) {
                Undo.RecordObjects(property.serializedObject.targetObjects, "Clear Level Condition");
                property.managedReferenceValue = null;
                property.serializedObject.ApplyModifiedProperties();
                return;
            }

            if (property.managedReferenceValue == null) return;
            DrawChildren(position, property, headerRect.yMax + spacing);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            var height = EditorGUIUtility.singleLineHeight;
            if (property.propertyType != SerializedPropertyType.ManagedReference || property.managedReferenceValue == null) return height;

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
                EditorGUI.PropertyField(new Rect(childX, y, childWidth, height), iterator, true);
                y += height + EditorGUIUtility.standardVerticalSpacing;
                if (!iterator.NextVisible(false)) break;
            }
        }

        private static GUIContent[] BuildOptions(IReadOnlyList<Type> types) {
            var labels = new GUIContent[types.Count + 1];
            labels[0] = new GUIContent("Select Condition");
            for (var i = 0; i < types.Count; i++) {
                labels[i + 1] = new GUIContent(LevelConditionTypeProvider.GetDisplayName(types[i]));
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
    }
}
