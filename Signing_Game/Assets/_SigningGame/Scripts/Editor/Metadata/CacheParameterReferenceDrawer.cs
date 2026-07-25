using System.Collections.Generic;
using System.Linq;
using Data.Modifiers;
using Utils.Metadata;

namespace SigningGame.Editor.Signatures.Metadata {
    #if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(CacheParameterReference))]
public sealed class CacheParameterReferenceDrawer
    : PropertyDrawer
{
    public override void OnGUI(
        Rect position,
        SerializedProperty property,
        GUIContent label
    ) {
        EditorGUI.BeginProperty(position, label, property);

        var groupProperty =
            property.FindPropertyRelative("_groupId");

        var parameterProperty =
            property.FindPropertyRelative("_parameterId");

        var wrappers =
            PredefinedMetadataWrapperStorage.Wrappers;

        var groupNames = wrappers
            .Select(wrapper => wrapper.DisplayName)
            .ToArray();

        var selectedGroupIndex = FindGroupIndex(
            wrappers,
            groupProperty.stringValue
        );

        var groupRect = new Rect(
            position.x,
            position.y,
            position.width,
            EditorGUIUtility.singleLineHeight
        );

        var newGroupIndex = EditorGUI.Popup(
            groupRect,
            "Cache group",
            selectedGroupIndex,
            groupNames
        );

        if (newGroupIndex >= 0 && newGroupIndex < wrappers.Count) {
            var selectedWrapper = wrappers[newGroupIndex];

            if (groupProperty.stringValue != selectedWrapper.GroupId) {
                groupProperty.stringValue = selectedWrapper.GroupId;
                parameterProperty.stringValue = string.Empty;
            }

            DrawParameterPopup(
                position,
                selectedWrapper,
                parameterProperty
            );
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(
        SerializedProperty property,
        GUIContent label
    ) {
        return EditorGUIUtility.singleLineHeight * 2f
               + EditorGUIUtility.standardVerticalSpacing;
    }

    private static int FindGroupIndex(IReadOnlyList<IModifiableWrapper> wrappers, string groupId) {
        for (var i = 0; i < wrappers.Count; i++) {
            if (wrappers[i].GroupId == groupId) {
                return i;
            }
        }

        return wrappers.Count > 0 ? 0 : -1;
    }

    private static void DrawParameterPopup(Rect position, IModifiableWrapper wrapper, SerializedProperty parameterProperty) {
        var parameters = wrapper.Parameters.ToArray();

        var parameterNames = parameters
            .Select(parameter => parameter.DisplayName)
            .ToArray();

        var selectedIndex = 0;

        for (var i = 0; i < parameters.Length; i++) {
            if (parameters[i].Key.ParameterId == parameterProperty.stringValue) {
                selectedIndex = i;
                break;
            }
        }

        var parameterRect = new Rect(
            position.x,
            position.y
                + EditorGUIUtility.singleLineHeight
                + EditorGUIUtility.standardVerticalSpacing,
            position.width,
            EditorGUIUtility.singleLineHeight
        );

        var newIndex = EditorGUI.Popup(
            parameterRect,
            "Parameter",
            selectedIndex,
            parameterNames
        );

        if (newIndex >= 0 && newIndex < parameters.Length) {
            parameterProperty.stringValue = parameters[newIndex].Key.ParameterId;
        }
    }
}

#endif
}