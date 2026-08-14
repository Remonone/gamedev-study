using System;
using System.Collections.Generic;
using System.Linq;
using Data.Modifiers;
using Data.Upgrades;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace SigningGame.Editor.UpgradeTree {
    internal sealed class UpgradeTreeEditorWindow : EditorWindow {
        private const float _ToolbarHeight = 24f;
        private const float _MinZoom = 0.2f;
        private const float _MaxZoom = 2.5f;
        private const float _MinInspectorWidth = 280f;
        private const float _SplitterWidth = 5f;

        private readonly List<UpgradeNodeDefinition> _nodes = new();
        private readonly HashSet<UpgradeNodeDefinition> _selection = new();
        private readonly List<string> _clipboardGuids = new();
        private readonly Dictionary<UpgradeNodeDefinition, Vector2> _dragStarts = new();
        private readonly HashSet<UpgradeNodeDefinition> _selectionBase = new();

        [SerializeField] private UpgradeTreeEditorMode _mode;

        private UpgradeTreeEditorSettings _settings;
        private UpgradeTreeEditorOperations _operations;
        private Vector2 _pan = new(320f, 220f);
        private float _zoom = 1f;
        private bool _gridEnabled = true;
        private float _inspectorWidth = 360f;
        private float _modifierPaneHeight = 220f;
        private Vector2 _inspectorScroll;
        private Vector2 _modifierScroll;
        private Vector2 _dragMouseStart;
        private Vector2 _selectionStart;
        private Rect _selectionRect;
        private int _hotControl;
        private Gesture _gesture;
        private UpgradeNodeDefinition _linkSource;
        private ModifierDefinition _selectedModifier;

        private string _search = string.Empty;
        private List<UpgradeNodeDefinition> _searchResults = new();
        private int _searchIndex = -1;
        private string _status = string.Empty;
        private MessageType _statusType = MessageType.Info;
        private string _stagedId = string.Empty;

        private PopupMode _popup;
        private string _popupText = string.Empty;
        private Vector2 _popupGraphPosition;
        private UpgradeNodeDefinition _popupLinkSource;
        private Rect _popupRect;

        private SettingsDraft _settingsDraft;

        [MenuItem("Tools/Signing Game/Upgrade Tree")]
        private static void OpenOrdinary() => Open(UpgradeTreeEditorMode.Ordinary);

        [MenuItem("Tools/Signing Game/Meta Upgrade Tree")]
        private static void OpenMeta() => Open(UpgradeTreeEditorMode.Meta);

        private static void Open(UpgradeTreeEditorMode mode) {
            var window = GetWindow<UpgradeTreeEditorWindow>();
            window.SwitchMode(mode);
            window.minSize = new Vector2(720f, 420f);
            window.Show();
        }

        private void OnEnable() {
            titleContent = new GUIContent(_mode == UpgradeTreeEditorMode.Meta ? "Meta Upgrade Tree" : "Upgrade Tree");
            _pan = new Vector2(EditorPrefs.GetFloat(Pref("PanX"), 320f), EditorPrefs.GetFloat(Pref("PanY"), 220f));
            _zoom = EditorPrefs.GetFloat(Pref("Zoom"), 1f);
            _gridEnabled = EditorPrefs.GetBool(Pref("Grid"), true);
            _inspectorWidth = EditorPrefs.GetFloat(Pref("InspectorWidth"), 360f);
            _modifierPaneHeight = EditorPrefs.GetFloat(Pref("ModifierHeight"), 220f);
            LoadSettings();
            EditorApplication.projectChanged += OnProjectChanged;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable() {
            CancelGesture();
            EditorApplication.projectChanged -= OnProjectChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorPrefs.SetFloat(Pref("PanX"), _pan.x);
            EditorPrefs.SetFloat(Pref("PanY"), _pan.y);
            EditorPrefs.SetFloat(Pref("Zoom"), _zoom);
            EditorPrefs.SetBool(Pref("Grid"), _gridEnabled);
            EditorPrefs.SetFloat(Pref("InspectorWidth"), _inspectorWidth);
            EditorPrefs.SetFloat(Pref("ModifierHeight"), _modifierPaneHeight);
        }

        private void OnGUI() {
            DrawToolbar();
            if (_settings == null || _operations == null) {
                EditorGUILayout.HelpBox(_status, MessageType.Error);
                return;
            }

            Rect canvas = GetCanvasRect();
            DrawCanvas(canvas);
            DrawInspector();
            DrawPopup();
            HandleCanvasInput(canvas);
            if (!string.IsNullOrEmpty(_status)) {
                Rect statusRect = new(8f, position.height - 25f, Mathf.Max(100f, canvas.width - 16f), 20f);
                EditorGUI.HelpBox(statusRect, _status, _statusType);
            }
            if (_gesture != Gesture.None) Repaint();
        }

        private void DrawToolbar() {
            bool popupActive = _popup != PopupMode.None;
            EditorGUI.BeginDisabledGroup(popupActive);
            GUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(_ToolbarHeight));
            if (GUILayout.Button("Add Node", EditorStyles.toolbarButton, GUILayout.Width(72f))) {
                OpenNodePopup(ScreenCenterToGraph(), null);
            }
            GUILayout.Space(4f);
            GUI.SetNextControlName("UpgradeTreeSearch");
            string search = GUILayout.TextField(_search, GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarTextField,
                GUILayout.MinWidth(120f), GUILayout.MaxWidth(260f));
            if (search != _search) {
                _search = search;
                RebuildSearch();
            }
            using (new EditorGUI.DisabledScope(_searchResults.Count == 0)) {
                if (GUILayout.Button("◀", EditorStyles.toolbarButton, GUILayout.Width(24f))) NavigateSearch(-1);
                if (GUILayout.Button("▶", EditorStyles.toolbarButton, GUILayout.Width(24f))) NavigateSearch(1);
            }
            GUILayout.Label(_searchResults.Count == 0 ? "0/0" : $"{_searchIndex + 1}/{_searchResults.Count}",
                EditorStyles.miniLabel, GUILayout.Width(45f));
            _gridEnabled = GUILayout.Toggle(_gridEnabled, "Grid", EditorStyles.toolbarButton, GUILayout.Width(48f));
            GUILayout.FlexibleSpace();
            if (_selection.Any(EditorUtility.IsDirty) &&
                GUILayout.Button("Save Selected", EditorStyles.toolbarButton, GUILayout.Width(92f))) {
                SaveObjects(_selection.Cast<UnityEngine.Object>());
            }
            if (EditorUtility.IsDirty(_settings) &&
                GUILayout.Button("Save Settings", EditorStyles.toolbarButton, GUILayout.Width(92f))) {
                SaveObjects(new UnityEngine.Object[] { _settings });
            }
            if (GUILayout.Button("Settings", EditorStyles.toolbarButton, GUILayout.Width(66f))) OpenSettingsPopup();
            GUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();
        }

        private void DrawCanvas(Rect canvas) {
            GUI.BeginClip(canvas);
            Rect local = new(0f, 0f, canvas.width, canvas.height);
            EditorGUI.DrawRect(local, new Color(0.105f, 0.105f, 0.105f, 1f));
            if (_gridEnabled) DrawGrid(local);
            DrawEdges(local);
            DrawNodes(local);
            if (_gesture == Gesture.Link && _linkSource != null) {
                Vector2 start = GraphToLocal(_linkSource.TreePosition);
                Handles.BeginGUI();
                DrawBezier(start, Event.current.mousePosition, _settings.PhantomPathColor, false, false);
                Handles.EndGUI();
            }
            if (_gesture == Gesture.Select) {
                EditorGUI.DrawRect(ToLocalRect(_selectionRect, canvas.position), new Color(0.25f, 0.55f, 1f, 0.15f));
                Handles.BeginGUI();
                Handles.color = new Color(0.35f, 0.7f, 1f, 0.9f);
                Handles.DrawSolidRectangleWithOutline(ToLocalRect(_selectionRect, canvas.position), Color.clear, Handles.color);
                Handles.EndGUI();
            }
            GUI.EndClip();
        }

        private void DrawGrid(Rect canvas) {
            float major = Mathf.Max(8f, _settings.GridSize * _zoom);
            while (major < 16f) major *= 5f;
            float x = Mathf.Repeat(_pan.x, major);
            float y = Mathf.Repeat(_pan.y, major);
            Color color = new(1f, 1f, 1f, 0.07f);
            for (; x < canvas.width; x += major) EditorGUI.DrawRect(new Rect(x, 0f, 1f, canvas.height), color);
            for (; y < canvas.height; y += major) EditorGUI.DrawRect(new Rect(0f, y, canvas.width, 1f), color);
        }

        private void DrawEdges(Rect canvas) {
            Handles.BeginGUI();
            foreach (UpgradeNodeDefinition source in _nodes) {
                foreach (UpgradeNodeLink link in source.Children ?? Array.Empty<UpgradeNodeLink>()) {
                    if (link.Child == null) continue;
                    DrawBezier(GraphToLocal(source.TreePosition), GraphToLocal(link.Child.TreePosition),
                        link.DrawEdge ? _settings.NormalPathColor : _settings.HiddenPathColor, true, link.DrawEdge);
                }
            }
            Handles.EndGUI();
        }

        private void DrawBezier(Vector2 start, Vector2 end, Color color, bool arrow, bool solid) {
            Vector2 delta = end - start;
            float tangent = Mathf.Max(36f, Mathf.Abs(delta.x) * 0.45f);
            Vector2 startTangent = start + Vector2.right * Mathf.Sign(delta.x == 0f ? 1f : delta.x) * tangent;
            Vector2 endTangent = end - Vector2.right * Mathf.Sign(delta.x == 0f ? 1f : delta.x) * tangent;
            Handles.DrawBezier(start, end, startTangent, endTangent, color, null, solid ? 3f : 2f);
            if (!arrow || delta.sqrMagnitude < 4f) return;
            Vector2 direction = (end - endTangent).normalized;
            Vector2 perpendicular = new(-direction.y, direction.x);
            Vector2 tip = end - direction * (_settings.NodeSize * _zoom * 0.5f);
            Handles.color = color;
            Handles.DrawAAConvexPolygon(tip, tip - direction * 10f + perpendicular * 5f, tip - direction * 10f - perpendicular * 5f);
        }

        private void DrawNodes(Rect canvas) {
            var style = new GUIStyle(EditorStyles.label) {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Max(8, Mathf.RoundToInt(_settings.IdFontSize * Mathf.Clamp(_zoom, 0.65f, 1.4f))),
                clipping = TextClipping.Clip,
                normal = { textColor = Color.white }
            };
            foreach (UpgradeNodeDefinition node in _nodes) {
                Rect rect = GetLocalNodeRect(node);
                bool selected = _selection.Contains(node);
                EditorGUI.DrawRect(rect, selected ? new Color(0.16f, 0.58f, 0.9f) : new Color(0.22f, 0.25f, 0.29f));
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2f), selected ? Color.cyan : new Color(0.45f, 0.5f, 0.55f));
                GUI.Label(rect, node.Id ?? "<no id>", style);
                if (EditorUtility.IsDirty(node)) GUI.Label(new Rect(rect.xMax - 14f, rect.y, 14f, 14f), "*", EditorStyles.boldLabel);
            }
            foreach (UpgradeNodeDefinition node in _selection) {
                if (node == null || !_nodes.Contains(node)) continue;
                Rect rect = GetLocalNodeRect(node);
                Vector2 size = style.CalcSize(new GUIContent(node.Id ?? "<no id>"));
                if (size.x <= rect.width) continue;
                Rect full = new(rect.center.x - size.x * 0.5f - 4f, rect.center.y - size.y * 0.5f,
                    size.x + 8f, size.y);
                EditorGUI.DrawRect(full, new Color(0.05f, 0.12f, 0.18f, 0.92f));
                var overflow = new GUIStyle(style) { clipping = TextClipping.Overflow };
                GUI.Label(full, node.Id, overflow);
            }
        }

        private void DrawInspector() {
            if (_selection.Count == 0) return;
            Rect panel = GetInspectorRect();
            Rect splitter = new(panel.x - _SplitterWidth, _ToolbarHeight, _SplitterWidth, position.height - _ToolbarHeight);
            EditorGUIUtility.AddCursorRect(splitter, MouseCursor.ResizeHorizontal);
            EditorGUI.DrawRect(panel, new Color(0.14f, 0.14f, 0.14f, 1f));
            EditorGUI.DrawRect(splitter, new Color(0.25f, 0.25f, 0.25f));
            if (_popup == PopupMode.None) HandleHorizontalSplitter(splitter);

            EditorGUI.BeginDisabledGroup(_popup != PopupMode.None);
            GUILayout.BeginArea(panel);
            GUILayout.Label($"Selection ({_selection.Count})", EditorStyles.boldLabel);
            if (_selection.Count != 1) {
                EditorGUILayout.HelpBox("Multiple nodes are selected. Node configuration and deletion are disabled.", MessageType.Info);
                GUILayout.EndArea();
                EditorGUI.EndDisabledGroup();
                return;
            }

            UpgradeNodeDefinition node = _selection.First();
            if (node == null) {
                GUILayout.EndArea();
                EditorGUI.EndDisabledGroup();
                return;
            }
            DrawSingleNodeInspector(node, panel.height);
            GUILayout.EndArea();
            EditorGUI.EndDisabledGroup();
        }

        private void DrawSingleNodeInspector(UpgradeNodeDefinition node, float height) {
            GUILayout.BeginHorizontal();
            GUILayout.Label(EditorUtility.IsDirty(node) ? "Unsaved node changes" : "Node saved", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(!EditorUtility.IsDirty(node))) {
                if (GUILayout.Button("Save", GUILayout.Width(48f))) SaveObjects(new UnityEngine.Object[] { node });
                if (GUILayout.Button("Revert", GUILayout.Width(52f)) &&
                    EditorUtility.DisplayDialog("Revert node", "Discard unsaved node changes?", "Revert", "Cancel")) {
                    RequestRevert(node);
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (string.IsNullOrEmpty(_stagedId)) _stagedId = node.Id;
            _stagedId = EditorGUILayout.TextField("ID", _stagedId);
            using (new EditorGUI.DisabledScope(string.Equals(_stagedId, node.Id, StringComparison.Ordinal))) {
                if (GUILayout.Button("Rename", GUILayout.Width(62f))) RenameNode(node);
            }
            GUILayout.EndHorizontal();

            float modifierHeight = _selectedModifier == null ? 0f : Mathf.Clamp(_modifierPaneHeight, 120f, height * 0.55f);
            _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll,
                GUILayout.Height(Mathf.Max(80f, height - modifierHeight - 88f)));
            var serialized = new SerializedObject(node);
            serialized.UpdateIfRequiredOrScript();
            DrawProperty(serialized, nameof(UpgradeNodeDefinition.Name));
            DrawProperty(serialized, nameof(UpgradeNodeDefinition.Description));
            DrawProperty(serialized, nameof(UpgradeNodeDefinition.MaxLevel));
            DrawProperty(serialized, nameof(UpgradeNodeDefinition.Icon));
            DrawProperty(serialized, nameof(UpgradeNodeDefinition.CostFormula));
            if (_mode == UpgradeTreeEditorMode.Ordinary) {
                DrawProperty(serialized, nameof(UpgradeNodeDefinition.FeatureUnlockIds));
                DrawProperty(serialized, nameof(UpgradeNodeDefinition.GrantsMetaCurrencyPoint));
            }
            DrawProperty(serialized, nameof(UpgradeNodeDefinition.LockedDisplayMode));
            DrawProperty(serialized, nameof(UpgradeNodeDefinition.ParentUnlockMode));
            if (_mode == UpgradeTreeEditorMode.Ordinary) {
                DrawProperty(serialized, nameof(UpgradeNodeDefinition.StatisticRequirementMode));
                DrawProperty(serialized, nameof(UpgradeNodeDefinition.StatisticRequirements));
            }
            serialized.ApplyModifiedProperties();

            EditorGUILayout.Space(5f);
            GUILayout.Label("Graph", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.Vector2Field("Position", node.TreePosition);
            DrawChildren(node);
            DrawModifiers(node);
            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(_selection.Count != 1)) {
                if (GUILayout.Button("Delete Node")) DeleteNode(node);
            }
            EditorGUILayout.EndScrollView();

            if (_selectedModifier != null) DrawModifierPane(modifierHeight);
        }

        private void DrawChildren(UpgradeNodeDefinition node) {
            GUILayout.Label($"Children ({(node.Children?.Length ?? 0)})", EditorStyles.boldLabel);
            foreach (UpgradeNodeLink link in node.Children ?? Array.Empty<UpgradeNodeLink>()) {
                if (link.Child == null) {
                    EditorGUILayout.HelpBox("Missing child reference", MessageType.Warning);
                    continue;
                }
                GUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(link.Child,
                    _mode == UpgradeTreeEditorMode.Meta ? typeof(MetaUpgradeNodeDefinition) : typeof(UpgradeNodeDefinition),
                    false);
                bool visible = GUILayout.Toggle(link.DrawEdge, "Visible", GUILayout.Width(60f));
                if (visible != link.DrawEdge) ShowResult(_operations.SetEdgeVisibility(node, link.Child, visible));
                if (GUILayout.Button("x", GUILayout.Width(22f))) {
                    ShowResult(_operations.RemoveEdge(node, link.Child));
                    GUILayout.EndHorizontal();
                    break;
                }
                GUILayout.EndHorizontal();
            }
        }

        private void DrawModifiers(UpgradeNodeDefinition node) {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Upgrade Effects", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+", GUILayout.Width(24f))) OpenModifierPopup(node);
            GUILayout.EndHorizontal();
            ModifierDefinition[] modifiers = node.Modifiers ?? Array.Empty<ModifierDefinition>();
            for (var index = 0; index < modifiers.Length; index++) {
                GUILayout.BeginHorizontal();
                ModifierDefinition current = modifiers[index];
                ModifierDefinition next = (ModifierDefinition)EditorGUILayout.ObjectField(current,
                    typeof(ModifierDefinition), false);
                if (next != current) ShowResult(_operations.SetModifierReference(node, index, next));
                if (GUILayout.Button("Edit", GUILayout.Width(38f))) _selectedModifier = current;
                if (GUILayout.Button("x", GUILayout.Width(22f))) {
                    ShowResult(_operations.RemoveModifierReference(node, index));
                    if (_selectedModifier == current) _selectedModifier = null;
                    GUILayout.EndHorizontal();
                    break;
                }
                GUILayout.EndHorizontal();
            }
        }

        private void DrawModifierPane(float modifierHeight) {
            Rect splitter = GUILayoutUtility.GetRect(1f, _SplitterWidth, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(splitter, new Color(0.3f, 0.3f, 0.3f));
            EditorGUIUtility.AddCursorRect(splitter, MouseCursor.ResizeVertical);
            if (_popup == PopupMode.None) HandleVerticalSplitter(splitter);
            GUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(modifierHeight));
            GUILayout.BeginHorizontal();
            GUILayout.Label(_selectedModifier.name, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(!EditorUtility.IsDirty(_selectedModifier))) {
                if (GUILayout.Button("Save", GUILayout.Width(48f))) SaveObjects(new UnityEngine.Object[] { _selectedModifier });
                if (GUILayout.Button("Revert", GUILayout.Width(52f)) &&
                    EditorUtility.DisplayDialog("Revert modifier", "Discard unsaved modifier changes?", "Revert", "Cancel")) {
                    RequestRevert(_selectedModifier);
                }
            }
            if (GUILayout.Button("Close", GUILayout.Width(44f))) _selectedModifier = null;
            GUILayout.EndHorizontal();
            if (_selectedModifier != null) {
                _modifierScroll = EditorGUILayout.BeginScrollView(_modifierScroll);
                var serialized = new SerializedObject(_selectedModifier);
                serialized.UpdateIfRequiredOrScript();
                SerializedProperty iterator = serialized.GetIterator();
                bool enter = true;
                while (iterator.NextVisible(enter)) {
                    enter = false;
                    if (iterator.propertyPath == "m_Script") continue;
                    int previousArraySize = iterator.propertyPath == nameof(ModifierDefinition.NumericModifiers)
                        ? iterator.arraySize
                        : -1;
                    EditorGUILayout.PropertyField(iterator, true);
                    if (previousArraySize >= 0 && iterator.arraySize > previousArraySize) {
                        InitializeNewNumericModifiers(iterator, previousArraySize);
                    }
                }
                serialized.ApplyModifiedProperties();
                EditorGUILayout.EndScrollView();
            }
            GUILayout.EndVertical();
        }

        private void DrawPopup() {
            if (_popup == PopupMode.None) return;
            if (_popup == PopupMode.Settings) {
                _popupRect = new Rect(Mathf.Max(8f, position.width * 0.5f - 230f), 54f, 460f, 355f);
            } else {
                _popupRect = new Rect(Mathf.Max(8f, position.width * 0.5f - 150f), 54f, 300f, 110f);
            }

            EditorGUI.DrawRect(new Rect(0f, _ToolbarHeight, position.width, position.height - _ToolbarHeight),
                new Color(0f, 0f, 0f, 0.35f));
            string title = _popup switch {
                PopupMode.Settings => _mode == UpgradeTreeEditorMode.Meta ? "Meta Upgrade Tree Settings" : "Upgrade Tree Settings",
                PopupMode.Node => _mode == UpgradeTreeEditorMode.Meta ? "New Meta Upgrade Node" : "New Upgrade Node",
                PopupMode.Modifier => "New Modifier Asset",
                _ => string.Empty
            };
            GUI.Box(_popupRect, title, GUI.skin.window);
            Rect content = new(_popupRect.x + 10f, _popupRect.y + 24f, _popupRect.width - 20f, _popupRect.height - 32f);
            GUILayout.BeginArea(content);
            if (_popup == PopupMode.Settings) DrawSettingsPopup();
            else DrawTextPopup();
            GUILayout.EndArea();
        }

        private void DrawTextPopup() {
            GUI.SetNextControlName("UpgradeTreePopupText");
            _popupText = EditorGUILayout.TextField(_popup == PopupMode.Node ? "Upgrade ID" : "Filename", _popupText);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel")) ClosePopup();
            if (GUILayout.Button("Create")) {
                if (_popup == PopupMode.Node) CreateNodeFromPopup();
                else CreateModifierFromPopup();
            }
            GUILayout.EndHorizontal();
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) {
                ClosePopup();
                Event.current.Use();
            }
        }

        private void DrawSettingsPopup() {
            if (_settingsDraft == null) _settingsDraft = new SettingsDraft(_settings, _mode);
            _settingsDraft.NodeSize = EditorGUILayout.FloatField("Node size", _settingsDraft.NodeSize);
            _settingsDraft.IdFontSize = EditorGUILayout.IntField("ID font size", _settingsDraft.IdFontSize);
            _settingsDraft.GridSize = EditorGUILayout.FloatField("Grid size", _settingsDraft.GridSize);
            _settingsDraft.Normal = EditorGUILayout.ColorField("Normal paths", _settingsDraft.Normal);
            _settingsDraft.Hidden = EditorGUILayout.ColorField("Hidden paths", _settingsDraft.Hidden);
            _settingsDraft.Phantom = EditorGUILayout.ColorField("Phantom path", _settingsDraft.Phantom);
            EditorGUILayout.Space(5f);
            GUILayout.Label("Storage", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Assets/", GUILayout.Width(48f));
            _settingsDraft.RootSuffix = EditorGUILayout.TextField(_settingsDraft.RootSuffix);
            GUILayout.EndHorizontal();
            EditorGUILayout.Space(5f);
            GUILayout.Label("Addressables", EditorStyles.boldLabel);
            _settingsDraft.Group = EditorGUILayout.TextField("Group", _settingsDraft.Group);
            EditorGUILayout.LabelField("Mandatory label", UpgradeTreeEditorSettings.GetMandatoryLabel(_mode));
            _settingsDraft.ExtraLabels = EditorGUILayout.TextField("Extra labels", _settingsDraft.ExtraLabels);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel")) ClosePopup();
            if (GUILayout.Button("Apply & Save")) ApplySettings();
            GUILayout.EndHorizontal();
        }

        private void HandleCanvasInput(Rect canvas) {
            Event evt = Event.current;
            if (evt.type == EventType.MouseLeaveWindow) {
                CancelGesture();
                return;
            }
            Rect inspector = GetInspectorRect();
            Rect inspectorInput = inspector == Rect.zero
                ? Rect.zero
                : new Rect(inspector.x - _SplitterWidth, inspector.y, inspector.width + _SplitterWidth, inspector.height);
            if (_popup != PopupMode.None || inspectorInput.Contains(evt.mousePosition) ||
                _gesture is Gesture.ResizeInspector or Gesture.ResizeModifier) return;

            if (evt.type == EventType.ScrollWheel && canvas.Contains(evt.mousePosition)) {
                Vector2 graph = ScreenToGraph(evt.mousePosition, canvas);
                float next = Mathf.Clamp(_zoom * Mathf.Pow(1.1f, -evt.delta.y), _MinZoom, _MaxZoom);
                _zoom = next;
                _pan = evt.mousePosition - canvas.position - graph * _zoom;
                evt.Use();
                Repaint();
                return;
            }

            if (evt.type == EventType.KeyDown && GUIUtility.keyboardControl == 0) {
                if (evt.keyCode == KeyCode.Escape) {
                    CancelGesture();
                    evt.Use();
                } else if ((evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace) &&
                           UpgradeTreeEditorOperations.CanDeleteSelection(_selection.Count)) {
                    DeleteNode(_selection.First());
                    evt.Use();
                } else if (IsActionKey(evt) && evt.keyCode == KeyCode.C) {
                    CopySelection();
                    evt.Use();
                } else if (IsActionKey(evt) && evt.keyCode == KeyCode.V) {
                    PasteClipboard();
                    evt.Use();
                }
                return;
            }

            if (evt.type == EventType.MouseDown && canvas.Contains(evt.mousePosition)) {
                UpgradeNodeDefinition hit = HitTest(evt.mousePosition, canvas);
                if (evt.button == 0 && hit != null && evt.clickCount == 2) {
                    FocusFolder(hit);
                    evt.Use();
                    return;
                }
                if (evt.button == 1 && hit != null) {
                    BeginGesture(Gesture.Link, evt.mousePosition);
                    _linkSource = hit;
                    evt.Use();
                    return;
                }
                if (evt.button != 0) return;
                if (hit != null) {
                    if (IsActionKey(evt)) {
                        if (!_selection.Add(hit)) _selection.Remove(hit);
                        OnSelectionChanged();
                    } else if (!_selection.Contains(hit)) {
                        SetSelection(hit);
                    }
                    if (_selection.Contains(hit)) BeginNodeDrag(evt.mousePosition);
                } else if (evt.shift) {
                    if (!IsActionKey(evt)) {
                        _selection.Clear();
                        OnSelectionChanged();
                    }
                    BeginGesture(Gesture.Select, evt.mousePosition);
                    _selectionBase.Clear();
                    foreach (UpgradeNodeDefinition selected in _selection) _selectionBase.Add(selected);
                    _selectionStart = evt.mousePosition;
                    _selectionRect = new Rect(evt.mousePosition, Vector2.zero);
                } else {
                    if (!IsActionKey(evt)) {
                        _selection.Clear();
                        OnSelectionChanged();
                    }
                    BeginGesture(Gesture.Pan, evt.mousePosition);
                }
                evt.Use();
            } else if (evt.type == EventType.MouseDrag && _gesture != Gesture.None) {
                if (_gesture == Gesture.Pan) {
                    _pan += evt.delta;
                } else if (_gesture == Gesture.DragNodes) {
                    Vector2 graphDelta = (evt.mousePosition - _dragMouseStart) / _zoom;
                    foreach (var pair in _dragStarts) {
                        Vector2 value = pair.Value + graphDelta;
                        if (_gridEnabled) {
                            float step = evt.shift ? _settings.GridSize / 10f : _settings.GridSize;
                            value = new Vector2(Mathf.Round(value.x / step) * step, Mathf.Round(value.y / step) * step);
                        }
                        pair.Key.TreePosition = value;
                        EditorUtility.SetDirty(pair.Key);
                    }
                } else if (_gesture == Gesture.Select) {
                    _selectionRect = Rect.MinMaxRect(
                        Mathf.Min(_selectionStart.x, evt.mousePosition.x), Mathf.Min(_selectionStart.y, evt.mousePosition.y),
                        Mathf.Max(_selectionStart.x, evt.mousePosition.x), Mathf.Max(_selectionStart.y, evt.mousePosition.y));
                    _selection.Clear();
                    foreach (UpgradeNodeDefinition selected in _selectionBase) _selection.Add(selected);
                    foreach (UpgradeNodeDefinition node in _nodes) {
                        if (_selectionRect.Overlaps(GetScreenNodeRect(node, canvas))) _selection.Add(node);
                    }
                    OnSelectionChanged();
                }
                evt.Use();
                Repaint();
            } else if (evt.type == EventType.MouseUp && _gesture != Gesture.None) {
                Gesture completed = _gesture;
                UpgradeNodeDefinition linkTarget = completed == Gesture.Link ? HitTest(evt.mousePosition, canvas) : null;
                EndGesture();
                if (completed == Gesture.Link && _linkSource != null) CompleteLink(linkTarget, evt.mousePosition, canvas);
                _linkSource = null;
                evt.Use();
            }
        }

        private void BeginNodeDrag(Vector2 mousePosition) {
            BeginGesture(Gesture.DragNodes, mousePosition);
            _dragMouseStart = mousePosition;
            _dragStarts.Clear();
            foreach (UpgradeNodeDefinition selected in _selection) {
                if (selected == null) continue;
                _dragStarts[selected] = selected.TreePosition;
            }
            Undo.RecordObjects(_dragStarts.Keys.Cast<UnityEngine.Object>().ToArray(), "Move upgrade nodes");
        }

        private void BeginGesture(Gesture gesture, Vector2 mousePosition) {
            _gesture = gesture;
            _hotControl = GUIUtility.GetControlID(FocusType.Passive);
            GUIUtility.hotControl = _hotControl;
            _dragMouseStart = mousePosition;
        }

        private void EndGesture() {
            if (GUIUtility.hotControl == _hotControl) GUIUtility.hotControl = 0;
            _hotControl = 0;
            _gesture = Gesture.None;
            _selectionBase.Clear();
        }

        private void CancelGesture() {
            if (_gesture == Gesture.DragNodes) {
                foreach (var pair in _dragStarts) {
                    if (pair.Key == null) continue;
                    pair.Key.TreePosition = pair.Value;
                    EditorUtility.SetDirty(pair.Key);
                }
            }
            EndGesture();
            _linkSource = null;
            _selectionBase.Clear();
        }

        private void CompleteLink(UpgradeNodeDefinition target, Vector2 mousePosition, Rect canvas) {
            if (_linkSource == null || target == _linkSource) return;
            if (target == null) {
                OpenNodePopup(ScreenToGraph(mousePosition, canvas), _linkSource);
                return;
            }
            bool exists = (_linkSource.Children ?? Array.Empty<UpgradeNodeLink>()).Any(link => link.Child == target);
            ShowResult(exists ? _operations.RemoveEdge(_linkSource, target) : _operations.AddEdge(_linkSource, target));
            ReloadNodes();
        }

        private void OpenNodePopup(Vector2 graphPosition, UpgradeNodeDefinition source) {
            _popup = PopupMode.Node;
            _popupText = string.Empty;
            _popupGraphPosition = graphPosition;
            _popupLinkSource = source;
            GUI.FocusControl("UpgradeTreePopupText");
            Repaint();
        }

        private void OpenModifierPopup(UpgradeNodeDefinition node) {
            if (node == null) return;
            _popup = PopupMode.Modifier;
            _popupText = string.Empty;
            _popupLinkSource = node;
            Repaint();
        }

        private void OpenSettingsPopup() {
            _settingsDraft = new SettingsDraft(_settings, _mode);
            _popup = PopupMode.Settings;
            Repaint();
        }

        private void ClosePopup() {
            _popup = PopupMode.None;
            _popupText = string.Empty;
            _popupLinkSource = null;
            _settingsDraft = null;
        }

        private void CreateNodeFromPopup() {
            UpgradeTreeOperationResult<UpgradeNodeDefinition> result =
                _operations.CreateNode(_popupText, _popupGraphPosition, _popupLinkSource);
            if (!result.Success) {
                SetStatus(result.Error, MessageType.Error);
                return;
            }
            ClosePopup();
            ReloadNodes();
            SetSelection(result.Value);
        }

        private void CreateModifierFromPopup() {
            UpgradeTreeOperationResult<ModifierDefinition> result =
                _operations.CreateModifier(_popupLinkSource, _popupText);
            if (!result.Success) {
                SetStatus(result.Error, MessageType.Error);
                return;
            }
            _selectedModifier = result.Value;
            ClosePopup();
            ReloadNodes();
        }

        private void ApplySettings() {
            if (!UpgradeTreeEditorValidation.TryValidateRootSuffix(_settingsDraft.RootSuffix,
                    out string root, out string error)) {
                SetStatus(error, MessageType.Error);
                return;
            }
            if (!AssetDatabase.CanOpenForEdit(_settings, out string message)) {
                SetStatus(message, MessageType.Error);
                return;
            }
            Undo.RecordObject(_settings, "Change Upgrade Tree settings");
            _settings.NodeSize = _settingsDraft.NodeSize;
            _settings.IdFontSize = _settingsDraft.IdFontSize;
            _settings.GridSize = _settingsDraft.GridSize;
            _settings.NormalPathColor = _settingsDraft.Normal;
            _settings.HiddenPathColor = _settingsDraft.Hidden;
            _settings.PhantomPathColor = _settingsDraft.Phantom;
            if (_mode == UpgradeTreeEditorMode.Meta) _settings.MetaUpgradeRootSuffix = root;
            else _settings.UpgradeRootSuffix = root;
            _settings.AddressablesGroup = _settingsDraft.Group?.Trim() ?? string.Empty;
            _settings.ExtraLabels = (_settingsDraft.ExtraLabels ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(label => label.Trim()).Where(label => label.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
            _settings.ClampValues();
            EditorUtility.SetDirty(_settings);
            AssetDatabase.SaveAssetIfDirty(_settings);
            _operations = new UpgradeTreeEditorOperations(_settings, AddressableAssetSettingsDefaultObject.Settings, _mode);
            ClosePopup();
            ReloadNodes();
        }

        private void RenameNode(UpgradeNodeDefinition node) {
            if (!EditorUtility.DisplayDialog("Rename upgrade",
                    $"Rename '{node.Id}' to '{_stagedId}'? Existing player saves use the old ID and are not migrated.",
                    "Rename", "Cancel")) return;
            UpgradeTreeOperationResult<UpgradeNodeDefinition> result = _operations.RenameNode(node, _stagedId);
            if (!result.Success) {
                SetStatus(result.Error, MessageType.Error);
                return;
            }
            ReloadNodes();
            SetSelection(result.Value);
        }

        private void DeleteNode(UpgradeNodeDefinition node) {
            if (!EditorUtility.DisplayDialog("Delete upgrade",
                    $"Delete '{node.Id}', its exclusively-owned modifiers and folder? Existing player saves may contain this ID; no save migration is performed.",
                    "Delete", "Cancel")) return;
            UpgradeTreeOperationResult<bool> result = _operations.DeleteNode(node);
            if (!result.Success) {
                SetStatus(result.Error, MessageType.Error);
                return;
            }
            _selection.Clear();
            _selectedModifier = null;
            ReloadNodes();
        }

        private void CopySelection() {
            _clipboardGuids.Clear();
            _clipboardGuids.AddRange(_selection.Where(node => node != null)
                .Select(node => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(node)))
                .OrderBy(guid => guid, StringComparer.Ordinal));
            SetStatus($"Copied {_clipboardGuids.Count} node(s).", MessageType.Info);
        }

        private void PasteClipboard() {
            var sources = new List<UpgradeNodeDefinition>();
            foreach (string guid in _clipboardGuids) {
                UpgradeNodeDefinition source = AssetDatabase.LoadAssetAtPath<UpgradeNodeDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (source == null) {
                    SetStatus("Paste cancelled because a clipboard source no longer exists.", MessageType.Error);
                    return;
                }
                sources.Add(source);
            }
            Vector2 offset = Vector2.one * (_gridEnabled ? _settings.GridSize : 40f);
            UpgradeTreeOperationResult<IReadOnlyList<UpgradeNodeDefinition>> result = _operations.CopyNodes(sources, offset);
            if (!result.Success) {
                SetStatus(result.Error, MessageType.Error);
                return;
            }
            ReloadNodes();
            _selection.Clear();
            foreach (UpgradeNodeDefinition copy in result.Value) _selection.Add(copy);
            OnSelectionChanged();
        }

        private void DrawProperty(SerializedObject serialized, string name) {
            SerializedProperty property = serialized.FindProperty(name);
            if (property != null) EditorGUILayout.PropertyField(property, true);
        }

        internal static void InitializeNewNumericModifiers(SerializedProperty modifiers, int firstNewIndex) {
            if (modifiers == null || !modifiers.isArray) return;
            for (int index = Mathf.Max(0, firstNewIndex); index < modifiers.arraySize; index++) {
                SerializedProperty modifier = modifiers.GetArrayElementAtIndex(index);
                SerializedProperty id = modifier.FindPropertyRelative("_id");
                SerializedProperty operation = modifier.FindPropertyRelative("_operation");
                SerializedProperty value = modifier.FindPropertyRelative("_value");
                SerializedProperty parameter = modifier.FindPropertyRelative("_parameter");
                if (id != null) id.stringValue = string.Empty;
                if (operation != null) operation.enumValueIndex = 0;
                if (value != null) value.managedReferenceValue = null;
                if (parameter == null) continue;
                SerializedProperty groupId = parameter.FindPropertyRelative("_groupId");
                SerializedProperty parameterId = parameter.FindPropertyRelative("_parameterId");
                if (groupId != null) groupId.stringValue = string.Empty;
                if (parameterId != null) parameterId.stringValue = string.Empty;
            }
        }

        private void SaveObjects(IEnumerable<UnityEngine.Object> objects) {
            foreach (UnityEngine.Object value in objects.Where(value => value != null).Distinct()) {
                if (!AssetDatabase.CanOpenForEdit(value, out string message)) {
                    SetStatus(message, MessageType.Error);
                    continue;
                }
                AssetDatabase.SaveAssetIfDirty(value);
            }
            SetStatus("Saved targeted assets.", MessageType.Info);
        }

        private void RequestRevert(UnityEngine.Object asset) {
            string path = AssetDatabase.GetAssetPath(asset);
            EditorApplication.delayCall += () => {
                if (string.IsNullOrEmpty(path)) return;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                ReloadNodes();
            };
        }

        private void FocusFolder(UpgradeNodeDefinition node) {
            string path = AssetDatabase.GetAssetPath(node);
            int slash = path.LastIndexOf('/');
            if (slash < 0) return;
            UnityEngine.Object folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path[..slash]);
            Selection.activeObject = folder;
            EditorGUIUtility.PingObject(folder);
            AssetDatabase.OpenAsset(folder);
        }

        private void LoadSettings() {
            _settings = UpgradeTreeEditorSettings.LoadOrCreate(out string error);
            if (_settings == null) {
                SetStatus(error, MessageType.Error);
                return;
            }
            _operations = new UpgradeTreeEditorOperations(_settings, AddressableAssetSettingsDefaultObject.Settings, _mode);
            ReloadNodes();
        }

        private void SwitchMode(UpgradeTreeEditorMode mode) {
            if (_mode == mode && _settings != null) {
                titleContent = new GUIContent(mode == UpgradeTreeEditorMode.Meta ? "Meta Upgrade Tree" : "Upgrade Tree");
                return;
            }
            CancelGesture();
            ClosePopup();
            _mode = mode;
            titleContent = new GUIContent(mode == UpgradeTreeEditorMode.Meta ? "Meta Upgrade Tree" : "Upgrade Tree");
            _selection.Clear();
            _selectedModifier = null;
            _settingsDraft = null;
            LoadSettings();
        }

        private void ReloadNodes() {
            _nodes.Clear();
            if (_operations != null) _nodes.AddRange(_operations.DiscoverNodes());
            _selection.RemoveWhere(node => node == null || !_nodes.Contains(node));
            OnSelectionChanged();
            RebuildSearch();
            Repaint();
        }

        private void RebuildSearch() {
            _searchResults = string.IsNullOrWhiteSpace(_search)
                ? new List<UpgradeNodeDefinition>()
                : _nodes.Where(node => (node.Id ?? string.Empty).IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       (node.Name ?? string.Empty).IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            _searchIndex = _searchResults.Count == 0 ? -1 : Mathf.Clamp(_searchIndex, 0, _searchResults.Count - 1);
        }

        private void NavigateSearch(int direction) {
            if (_searchResults.Count == 0) return;
            _searchIndex = (_searchIndex + direction + _searchResults.Count) % _searchResults.Count;
            UpgradeNodeDefinition node = _searchResults[_searchIndex];
            SetSelection(node);
            Rect canvas = GetCanvasRect();
            _pan = canvas.size * 0.5f - node.TreePosition * _zoom;
            Repaint();
        }

        private void SetSelection(UpgradeNodeDefinition node) {
            _selection.Clear();
            if (node != null) _selection.Add(node);
            _stagedId = node?.Id ?? string.Empty;
            OnSelectionChanged();
        }

        private void OnSelectionChanged() {
            if (_selection.Count != 1) {
                _selectedModifier = null;
                return;
            }
            UpgradeNodeDefinition selectedNode = _selection.First();
            if (_selectedModifier != null &&
                !(selectedNode.Modifiers ?? Array.Empty<ModifierDefinition>()).Contains(_selectedModifier)) {
                _selectedModifier = null;
            }
            _stagedId = selectedNode.Id ?? string.Empty;
        }

        private UpgradeNodeDefinition HitTest(Vector2 screen, Rect canvas) {
            for (var index = _nodes.Count - 1; index >= 0; index--) {
                if (GetScreenNodeRect(_nodes[index], canvas).Contains(screen)) return _nodes[index];
            }
            return null;
        }

        private Rect GetCanvasRect() {
            return new Rect(0f, _ToolbarHeight, position.width, position.height - _ToolbarHeight);
        }

        private Rect GetInspectorRect() {
            if (_selection.Count == 0) return Rect.zero;
            float width = Mathf.Clamp(_inspectorWidth, _MinInspectorWidth, Mathf.Max(_MinInspectorWidth, position.width - 180f));
            return new Rect(position.width - width, _ToolbarHeight, width, position.height - _ToolbarHeight);
        }

        private Rect GetLocalNodeRect(UpgradeNodeDefinition node) {
            float size = _settings.NodeSize * _zoom;
            Vector2 center = GraphToLocal(node.TreePosition);
            return new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size);
        }

        private Rect GetScreenNodeRect(UpgradeNodeDefinition node, Rect canvas) {
            Rect local = GetLocalNodeRect(node);
            local.position += canvas.position;
            return local;
        }

        private Vector2 GraphToLocal(Vector2 graph) => _pan + graph * _zoom;
        private Vector2 ScreenToGraph(Vector2 screen, Rect canvas) => (screen - canvas.position - _pan) / _zoom;
        private Vector2 ScreenCenterToGraph() => ScreenToGraph(GetCanvasRect().center, GetCanvasRect());

        private void HandleHorizontalSplitter(Rect splitter) {
            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0 && splitter.Contains(evt.mousePosition)) {
                BeginGesture(Gesture.ResizeInspector, evt.mousePosition);
                evt.Use();
            } else if (_gesture == Gesture.ResizeInspector && evt.type == EventType.MouseDrag) {
                _inspectorWidth = Mathf.Clamp(position.width - evt.mousePosition.x, _MinInspectorWidth, position.width - 180f);
                evt.Use();
                Repaint();
            } else if (_gesture == Gesture.ResizeInspector && evt.type == EventType.MouseUp) {
                EndGesture();
                evt.Use();
            }
        }

        private void HandleVerticalSplitter(Rect splitter) {
            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0 && splitter.Contains(evt.mousePosition)) {
                BeginGesture(Gesture.ResizeModifier, evt.mousePosition);
                evt.Use();
            } else if (_gesture == Gesture.ResizeModifier && evt.type == EventType.MouseDrag) {
                _modifierPaneHeight = Mathf.Clamp(_modifierPaneHeight - evt.delta.y, 120f, position.height * 0.65f);
                evt.Use();
                Repaint();
            } else if (_gesture == Gesture.ResizeModifier && evt.type == EventType.MouseUp) {
                EndGesture();
                evt.Use();
            }
        }

        private void OnProjectChanged() => ReloadNodes();
        private void OnUndoRedo() => ReloadNodes();

        private void ShowResult<T>(UpgradeTreeOperationResult<T> result) {
            SetStatus(result.Success ? "Operation completed." : result.Error,
                result.Success ? MessageType.Info : MessageType.Error);
        }

        private void SetStatus(string message, MessageType type) {
            _status = message ?? string.Empty;
            _statusType = type;
            Repaint();
        }

        private static Rect ToLocalRect(Rect screen, Vector2 canvasPosition) {
            screen.position -= canvasPosition;
            return screen;
        }

        private static bool IsActionKey(Event evt) => evt.control || evt.command;

        private string Pref(string key) => $"SigningGame.{_mode}.UpgradeTree.{key}";

        private enum Gesture {
            None,
            Pan,
            DragNodes,
            Select,
            Link,
            ResizeInspector,
            ResizeModifier
        }

        private enum PopupMode {
            None,
            Node,
            Modifier,
            Settings
        }

        private sealed class SettingsDraft {
            internal float NodeSize;
            internal int IdFontSize;
            internal float GridSize;
            internal Color Normal;
            internal Color Hidden;
            internal Color Phantom;
            internal string RootSuffix;
            internal string Group;
            internal string ExtraLabels;

            internal SettingsDraft(UpgradeTreeEditorSettings source, UpgradeTreeEditorMode mode = UpgradeTreeEditorMode.Ordinary) {
                NodeSize = source.NodeSize;
                IdFontSize = source.IdFontSize;
                GridSize = source.GridSize;
                Normal = source.NormalPathColor;
                Hidden = source.HiddenPathColor;
                Phantom = source.PhantomPathColor;
                RootSuffix = source.GetRootSuffix(mode);
                Group = source.AddressablesGroup;
                ExtraLabels = string.Join(", ", source.ExtraLabels ?? Array.Empty<string>());
            }
        }
    }
}
