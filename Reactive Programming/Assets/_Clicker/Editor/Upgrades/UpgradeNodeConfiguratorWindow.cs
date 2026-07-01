using System;
using System.Collections.Generic;
using System.Linq;
using Types.Upgrades;
using Types.Upgrades.Effects;
using UnityEditor;
using UnityEngine;

namespace Clicker.Editor.Upgrades {
    internal sealed class UpgradeNodeConfiguratorWindow : EditorWindow {
        private readonly UpgradeEffectListDrawer _effectListDrawer = new();
        private SerializedObject _serializedNode;
        private UpgradeNodeDefinition _node;
        private string _stagedId = string.Empty;
        private Vector2 _scroll;

        [MenuItem("Clicker/Upgrade Tree/Node Configurator")]
        private static void Open() {
            GetWindow<UpgradeNodeConfiguratorWindow>("Upgrade Node");
        }

        private void OnEnable() {
            UpgradeTreeSelection.SelectionChanged += OnSelectionChanged;
            OnSelectionChanged();
        }

        private void OnDisable() {
            UpgradeTreeSelection.SelectionChanged -= OnSelectionChanged;
            _effectListDrawer.Dispose();
        }

        private void OnGUI() {
            var selected = UpgradeTreeSelection.SelectedNodes;
            if (selected.Count != 1) {
                EditorGUILayout.HelpBox(selected.Count == 0
                    ? "Select one upgrade node in the tree editor."
                    : "Multiple nodes selected. The configurator is empty because it cannot decide which node to edit.", MessageType.Info);
                return;
            }

            if (_node != selected[0] || _serializedNode == null) {
                SetNode(selected[0]);
            }

            if (_node == null) return;

            var nodes = UpgradeTreeAssetUtility.LoadNodes();
            if (UpgradeTreeAssetUtility.HasDuplicateIds(nodes, out var duplicateMessage)) {
                EditorGUILayout.HelpBox(duplicateMessage + ". Id apply/copy/create operations are blocked until duplicate ids are fixed.", MessageType.Warning);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _serializedNode.Update();

            DrawIdEditor(nodes);
            EditorGUILayout.Space(6f);
            DrawProperty("MaxLevel");
            DrawProperty("Name");
            DrawProperty("Icon");
            DrawProperty("Description");
            DrawProperty("NodeCategory");
            DrawProperty("Price", true);

            EditorGUILayout.Space(8f);
            DrawReadOnlyDerivedFields();

            EditorGUILayout.Space(8f);
            _effectListDrawer.Draw(_node, _serializedNode.FindProperty("Effects"));

            _serializedNode.ApplyModifiedProperties();
            EditorGUILayout.EndScrollView();
        }

        private void DrawIdEditor(IReadOnlyList<UpgradeNodeDefinition> nodes) {
            EditorGUILayout.LabelField("Runtime/save key", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Changing Id updates graph ChildIds, but Id is also used as a runtime/save key and can invalidate existing saves.", MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope()) {
                _stagedId = EditorGUILayout.TextField("Id", _stagedId);
                var duplicateIds = UpgradeTreeAssetUtility.HasDuplicateIds(nodes, out _);
                var canApply = !duplicateIds && _stagedId != _node.Id && UpgradeTreeAssetUtility.IsIdUnique(_stagedId, _node, nodes);
                using (new EditorGUI.DisabledScope(!canApply)) {
                    if (GUILayout.Button("Apply", GUILayout.Width(70f))) {
                        UpgradeTreeAssetUtility.ApplyNodeIdChange(_node, _stagedId, nodes);
                        _serializedNode = new SerializedObject(_node);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(_stagedId)) {
                EditorGUILayout.HelpBox("Id cannot be empty.", MessageType.Error);
            }
            else if (!UpgradeTreeAssetUtility.IsIdUnique(_stagedId, _node, nodes) && _stagedId != _node.Id) {
                EditorGUILayout.HelpBox("Id must be unique among runtime upgrade nodes.", MessageType.Error);
            }
        }

        private void DrawReadOnlyDerivedFields() {
            using (new EditorGUI.DisabledScope(true)) {
                EditorGUILayout.Vector2Field("Position (tree controlled)", _node.Position);
                var childIds = _node.ChildIds == null ? string.Empty : string.Join(", ", _node.ChildIds);
                EditorGUILayout.TextField("Child Ids (links controlled)", childIds);
            }
        }

        private void DrawProperty(string propertyName, bool includeChildren = false) {
            var property = _serializedNode.FindProperty(propertyName);
            if (property != null) {
                EditorGUILayout.PropertyField(property, includeChildren);
            }
        }

        private void OnSelectionChanged() {
            var selected = UpgradeTreeSelection.SelectedNodes;
            SetNode(selected.Count == 1 ? selected[0] : null);
            Repaint();
        }

        private void SetNode(UpgradeNodeDefinition node) {
            _effectListDrawer.ClearEditors();
            _node = node;
            _serializedNode = node == null ? null : new SerializedObject(node);
            _stagedId = node == null ? string.Empty : node.Id;
        }
    }

    internal sealed class UpgradeEffectListDrawer : IDisposable {
        private readonly Dictionary<UpgradeEffect, UnityEditor.Editor> _editors = new();

        public void Draw(UpgradeNodeDefinition node, SerializedProperty effectsProperty) {
            if (node == null || effectsProperty == null) return;

            EditorGUILayout.LabelField("Upgrade Effects", EditorStyles.boldLabel);
            if (effectsProperty.arraySize == 0) {
                EditorGUILayout.HelpBox("No effects configured.", MessageType.Info);
            }

            for (var i = 0; i < effectsProperty.arraySize; i++) {
                var element = effectsProperty.GetArrayElementAtIndex(i);
                var effect = element.objectReferenceValue as UpgradeEffect;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                    using (new EditorGUILayout.HorizontalScope()) {
                        EditorGUILayout.PropertyField(element, new GUIContent($"Effect {i}"));
                        if (GUILayout.Button("Select", GUILayout.Width(54f)) && effect != null) {
                            Selection.activeObject = effect;
                            EditorGUIUtility.PingObject(effect);
                        }

                        if (GUILayout.Button("-", GUILayout.Width(24f))) {
                            effectsProperty.DeleteArrayElementAtIndex(i);
                            break;
                        }
                    }

                    if (effect != null) {
                        DrawInlineEditor(effect);
                    }
                }
            }

            if (GUILayout.Button("Add Effect")) {
                ShowAddEffectMenu(node);
            }
        }

        public void Dispose() {
            ClearEditors();
        }

        public void ClearEditors() {
            foreach (var editor in _editors.Values) {
                if (editor != null) {
                    UnityEngine.Object.DestroyImmediate(editor);
                }
            }

            _editors.Clear();
        }

        private void DrawInlineEditor(UpgradeEffect effect) {
            if (!_editors.TryGetValue(effect, out var editor) || editor == null) {
                UnityEditor.Editor.CreateCachedEditor(effect, null, ref editor);
                _editors[effect] = editor;
            }

            using (new EditorGUI.IndentLevelScope()) {
                editor.OnInspectorGUI();
            }
        }

        private void ShowAddEffectMenu(UpgradeNodeDefinition node) {
            var menu = new GenericMenu();
            var types = TypeCache.GetTypesDerivedFrom<UpgradeEffect>()
                .Where(type => !type.IsAbstract)
                .OrderBy(type => type.Name);

            foreach (var type in types) {
                menu.AddItem(new GUIContent(type.Name), false, () => UpgradeTreeAssetUtility.CreateEffect(node, type));
            }

            menu.ShowAsContext();
        }
    }
}
