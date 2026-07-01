using System;
using System.Linq;
using Types.Modifiers.Definitions;
using Types.Upgrades.Effects;
using UnityEditor;
using UnityEngine;

namespace Clicker.Editor.Upgrades {
    [CustomEditor(typeof(ModifierUpgradeEffect))]
    internal sealed class ModifierUpgradeEffectEditor : UnityEditor.Editor {
        private SerializedProperty _rulesProperty;

        private void OnEnable() {
            _rulesProperty = serializedObject.FindProperty("Rules");
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();

            var iterator = serializedObject.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren)) {
                enterChildren = false;
                if (iterator.propertyPath == "m_Script" || iterator.propertyPath == "Rules") continue;
                EditorGUILayout.PropertyField(iterator, true);
            }

            DrawRules();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawRules() {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Modifier Definitions", EditorStyles.boldLabel);

            if (_rulesProperty == null) {
                EditorGUILayout.HelpBox("Rules list is missing.", MessageType.Warning);
                return;
            }

            for (var i = 0; i < _rulesProperty.arraySize; i++) {
                var element = _rulesProperty.GetArrayElementAtIndex(i);
                var modifier = element.objectReferenceValue as ModifierDefinition;
                var rowRect = EditorGUILayout.GetControlRect();
                var fieldRect = new Rect(rowRect.x, rowRect.y, rowRect.width - 84f, rowRect.height);
                var selectRect = new Rect(fieldRect.xMax + 4f, rowRect.y, 54f, rowRect.height);
                var removeRect = new Rect(selectRect.xMax + 4f, rowRect.y, 22f, rowRect.height);

                EditorGUI.PropertyField(fieldRect, element, new GUIContent($"Rule {i}"));
                if (GUI.Button(selectRect, "Select") && modifier != null) {
                    OpenModifierInInspector(modifier);
                }

                if (GUI.Button(removeRect, "-")) {
                    _rulesProperty.DeleteArrayElementAtIndex(i);
                    break;
                }

                if (modifier != null && Event.current.type == EventType.MouseDown && Event.current.button == 0 && rowRect.Contains(Event.current.mousePosition)) {
                    OpenModifierInInspector(modifier);
                    Event.current.Use();
                }
            }

            var plusRect = GUILayoutUtility.GetRect(new GUIContent("+"), GUI.skin.button, GUILayout.Width(28f));
            if (GUI.Button(plusRect, "+")) {
                PopupWindow.Show(plusRect, new ModifierCreatePopup((ModifierUpgradeEffect)target));
            }
        }

        private static void OpenModifierInInspector(ModifierDefinition modifier) {
            Selection.activeObject = modifier;
            EditorGUIUtility.PingObject(modifier);

            var inspectorType = Type.GetType("UnityEditor.InspectorWindow,UnityEditor");
            var focusMethod = typeof(EditorWindow).GetMethods()
                .FirstOrDefault(method => method.Name == "FocusWindowIfItsOpen"
                                          && !method.IsGenericMethod
                                          && method.GetParameters().Length == 1
                                          && method.GetParameters()[0].ParameterType == typeof(Type));
            if (inspectorType != null && focusMethod != null) {
                focusMethod.Invoke(null, new object[] { inspectorType });
            }

            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }
    }

    internal sealed class ModifierCreatePopup : PopupWindowContent {
        private readonly ModifierUpgradeEffect _effect;
        private readonly Type[] _types;
        private string _modifierId = string.Empty;
        private int _selectedTypeIndex;

        public ModifierCreatePopup(ModifierUpgradeEffect effect) {
            _effect = effect;
            _types = TypeCache.GetTypesDerivedFrom<ModifierDefinition>()
                .Where(type => !type.IsAbstract)
                .OrderBy(type => type.Name)
                .ToArray();
        }

        public override Vector2 GetWindowSize() {
            return new Vector2(280f, 92f);
        }

        public override void OnGUI(Rect rect) {
            GUILayout.Label("Create Modifier", EditorStyles.boldLabel);
            _modifierId = EditorGUILayout.TextField("Id", _modifierId);

            using (new EditorGUI.DisabledScope(_types.Length == 0)) {
                _selectedTypeIndex = EditorGUILayout.Popup("Type", _selectedTypeIndex, _types.Select(type => type.Name).ToArray());
                if (GUILayout.Button("Create")) {
                    if (_types.Length > 0) {
                        UpgradeTreeAssetUtility.CreateModifier(_effect, _modifierId, _types[_selectedTypeIndex]);
                    }
                    editorWindow.Close();
                }
            }
        }
    }
}
