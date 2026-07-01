using System.Collections.Generic;
using System.IO;
using System.Linq;
using Types.Upgrades;
using UnityEditor;
using UnityEngine;

namespace Clicker.Editor.Upgrades {
    internal sealed class UpgradeTreeEditorWindow : EditorWindow {
        private const string SearchControlName = "UpgradeTreeSearchField";
        private const float GridSmall = 32f;
        private const float GridLarge = 160f;
        private const float MinZoom = 0.35f;
        private const float MaxZoom = 2.5f;
        private const float ZoomSensitivity = 0.08f;
        private static readonly Vector2 CopyOffset = new(48f, 48f);

        private readonly List<UpgradeNodeDefinition> _nodes = new();
        private readonly List<UpgradeNodeDefinition> _clipboard = new();
        private readonly List<UpgradeNodeDefinition> _searchResults = new();
        private Vector2 _pan;
        private float _zoom = 1f;
        private string _search = string.Empty;
        private int _searchIndex;
        private UpgradeNodeDefinition _dragNode;
        private UpgradeNodeDefinition _phantomSource;
        private Vector2 _lastMouse;
        private Vector2 _dragStartMouse;
        private bool _isPanning;
        private bool _isDraggingNodes;
        private bool _isBoxSelecting;
        private bool _dragMoved;
        private Vector2 _boxSelectionStart;
        private Vector2 _boxSelectionCurrent;
        private UpgradeTreeSettings _settings;
        private GUIStyle _nodeLabelStyle;
        private GUIStyle _selectedLabelStyle;

        [MenuItem("Clicker/Upgrade Tree/Tree Editor")]
        private static void Open() {
            GetWindow<UpgradeTreeEditorWindow>("Upgrade Tree");
        }

        private void OnEnable() {
            _settings = UpgradeTreeSettings.GetOrCreate();
            RefreshNodes();
            UpgradeTreeSelection.SelectionChanged += Repaint;
        }

        private void OnDisable() {
            UpgradeTreeSelection.SelectionChanged -= Repaint;
        }

        private void OnGUI() {
            _settings = _settings == null ? UpgradeTreeSettings.GetOrCreate() : _settings;
            UpdateStyles();
            HandleKeyboard(Event.current);
            DrawToolbar();

            var graphRect = new Rect(0f, EditorGUIUtility.singleLineHeight + 8f, position.width, position.height - EditorGUIUtility.singleLineHeight - 8f);
            DrawGraph(graphRect);
            HandleGraphInput(graphRect, Event.current);

            if (Event.current.type == EventType.Repaint && UpgradeTreeAssetUtility.HasDuplicateIds(_nodes, out var duplicateMessage)) {
                var rect = new Rect(graphRect.x + 8f, graphRect.y + 8f, graphRect.width - 16f, 34f);
                EditorGUI.HelpBox(rect, duplicateMessage + ". Create/copy/id operations are blocked until fixed.", MessageType.Warning);
            }
        }

        private void DrawToolbar() {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                if (GUILayout.Button("Add Node", EditorStyles.toolbarButton, GUILayout.Width(80f))) {
                    TryCreateNode(ScreenToWorld(new Vector2(position.width * 0.5f, position.height * 0.5f)));
                }

                GUILayout.Space(8f);
                EditorGUI.BeginChangeCheck();
                GUI.SetNextControlName(SearchControlName);
                _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(160f));
                if (EditorGUI.EndChangeCheck()) {
                    RefreshSearchResults();
                }

                if (GUILayout.Button("Prev", EditorStyles.toolbarButton, GUILayout.Width(48f))) MoveSearch(-1);
                if (GUILayout.Button("Next", EditorStyles.toolbarButton, GUILayout.Width(48f))) MoveSearch(1);
                GUILayout.Label($"{(_searchResults.Count == 0 ? 0 : _searchIndex + 1)}/{_searchResults.Count}", GUILayout.Width(48f));

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Open Configurator", EditorStyles.toolbarButton, GUILayout.Width(130f))) {
                    GetWindow<UpgradeNodeConfiguratorWindow>("Upgrade Node");
                }

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(64f))) {
                    RefreshNodes();
                }
            }
        }

        private void DrawGraph(Rect rect) {
            GUI.Box(rect, GUIContent.none);
            DrawGrid(rect, GridSmall, new Color(1f, 1f, 1f, 0.08f));
            DrawGrid(rect, GridLarge, new Color(1f, 1f, 1f, 0.16f));
            DrawLinks(rect);
            DrawPhantomLink();
            DrawNodes(rect);
            DrawBoxSelection();
        }

        private void DrawGrid(Rect rect, float spacing, Color color) {
            Handles.BeginGUI();
            Handles.color = color;

            var scaledSpacing = Mathf.Max(4f, spacing * _zoom);
            var offset = new Vector2(_pan.x % scaledSpacing, _pan.y % scaledSpacing);
            for (var x = rect.x + offset.x; x < rect.xMax; x += scaledSpacing) {
                Handles.DrawLine(new Vector3(x, rect.y), new Vector3(x, rect.yMax));
            }

            for (var y = rect.y + offset.y; y < rect.yMax; y += scaledSpacing) {
                Handles.DrawLine(new Vector3(rect.x, y), new Vector3(rect.xMax, y));
            }

            Handles.EndGUI();
        }

        private void DrawLinks(Rect rect) {
            var byId = new Dictionary<string, UpgradeNodeDefinition>();
            foreach (var node in _nodes) {
                if (node != null && !string.IsNullOrWhiteSpace(node.Id) && !byId.ContainsKey(node.Id)) {
                    byId.Add(node.Id, node);
                }
            }

            Handles.BeginGUI();
            Handles.color = _settings.PathColor;
            foreach (var node in _nodes) {
                if (node == null || node.ChildIds == null) continue;
                var from = GetNodeRect(node).center;
                foreach (var childId in node.ChildIds) {
                    if (!byId.TryGetValue(childId, out var child) || child == null) continue;
                    var to = GetNodeRect(child).center;
                    Handles.DrawAAPolyLine(3f, from, to);
                    DrawArrowHead(from, to);
                }
            }
            Handles.EndGUI();
        }

        private void DrawPhantomLink() {
            if (_phantomSource == null) return;

            Handles.BeginGUI();
            Handles.color = new Color(_settings.PathColor.r, _settings.PathColor.g, _settings.PathColor.b, 0.5f);
            var from = GetNodeRect(_phantomSource).center;
            Handles.DrawAAPolyLine(4f, from, Event.current.mousePosition);
            Handles.EndGUI();
        }

        private void DrawArrowHead(Vector2 from, Vector2 to) {
            var direction = (to - from).normalized;
            if (direction == Vector2.zero) return;

            var right = new Vector2(-direction.y, direction.x);
            var tip = to - direction * (GetScreenNodeSize() * 0.5f);
            var a = tip - direction * 12f + right * 6f;
            var b = tip - direction * 12f - right * 6f;
            Handles.DrawAAConvexPolygon(tip, a, b);
        }

        private void DrawBoxSelection() {
            if (!_isBoxSelecting) return;

            var rect = GetNormalizedRect(_boxSelectionStart, _boxSelectionCurrent);
            EditorGUI.DrawRect(rect, new Color(0.25f, 0.55f, 1f, 0.18f));
            Handles.BeginGUI();
            Handles.color = new Color(0.35f, 0.75f, 1f, 0.9f);
            Handles.DrawAAPolyLine(2f,
                new Vector2(rect.xMin, rect.yMin),
                new Vector2(rect.xMax, rect.yMin),
                new Vector2(rect.xMax, rect.yMax),
                new Vector2(rect.xMin, rect.yMax),
                new Vector2(rect.xMin, rect.yMin));
            Handles.EndGUI();
        }

        private void DrawNodes(Rect graphRect) {
            foreach (var node in _nodes) {
                if (node == null) continue;

                var rect = GetNodeRect(node);
                var selected = UpgradeTreeSelection.Contains(node);
                EditorGUI.DrawRect(rect, selected ? new Color(0.25f, 0.45f, 0.8f) : new Color(0.22f, 0.22f, 0.22f));
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2f), selected ? Color.cyan : Color.gray);
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), selected ? Color.cyan : Color.gray);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 2f, rect.height), selected ? Color.cyan : Color.gray);
                EditorGUI.DrawRect(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), selected ? Color.cyan : Color.gray);

                var labelRect = rect.Shrink(5f);
                GUI.Label(labelRect, node.Id, _nodeLabelStyle);

                if (selected) {
                    var size = _selectedLabelStyle.CalcSize(new GUIContent(node.Id));
                    var fullRect = new Rect(rect.center.x - size.x * 0.5f, rect.y - size.y - 4f, size.x + 8f, size.y + 2f);
                    EditorGUI.DrawRect(fullRect, new Color(0f, 0f, 0f, 0.75f));
                    GUI.Label(fullRect, node.Id, _selectedLabelStyle);
                }
            }
        }

        private void HandleGraphInput(Rect graphRect, Event evt) {
            var operationActive = _isPanning || _isDraggingNodes || _phantomSource != null || _isBoxSelecting;
            if (!operationActive && !graphRect.Contains(evt.mousePosition)) return;

            if (evt.type is EventType.MouseLeaveWindow or EventType.Ignore) {
                ResetActiveOperations();
                return;
            }

            if (evt.type == EventType.ScrollWheel && graphRect.Contains(evt.mousePosition)) {
                ZoomAt(evt.mousePosition, evt.delta.y);
                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseDown && evt.button == 0) {
                if (!graphRect.Contains(evt.mousePosition)) return;
                if (evt.shift) {
                    _isBoxSelecting = true;
                    _isPanning = false;
                    _isDraggingNodes = false;
                    _phantomSource = null;
                    _boxSelectionStart = evt.mousePosition;
                    _boxSelectionCurrent = evt.mousePosition;
                    evt.Use();
                    return;
                }

                _dragNode = GetNodeAt(evt.mousePosition);
                _lastMouse = evt.mousePosition;
                _dragStartMouse = evt.mousePosition;
                _dragMoved = false;
                if (_dragNode != null) {
                    if (evt.clickCount == 2) {
                        RevealNodeFolder(_dragNode);
                        evt.Use();
                        return;
                    }

                    if (evt.control || evt.command) {
                        UpgradeTreeSelection.ToggleNode(_dragNode);
                    }
                    else if (!UpgradeTreeSelection.Contains(_dragNode)) {
                        UpgradeTreeSelection.SetSelection(_dragNode);
                    }

                    GetWindow<UpgradeNodeConfiguratorWindow>("Upgrade Node");

                    _isDraggingNodes = true;
                }
                else {
                    if (!evt.control && !evt.command) UpgradeTreeSelection.SetSelection((UpgradeNodeDefinition)null);
                    _isPanning = true;
                }
                evt.Use();
            }

            if (evt.type == EventType.MouseDown && evt.button == 1) {
                if (!graphRect.Contains(evt.mousePosition)) return;
                _phantomSource = GetNodeAt(evt.mousePosition);
                if (_phantomSource != null) evt.Use();
            }

            if (evt.type == EventType.MouseDrag && evt.button == 0) {
                var delta = evt.mousePosition - _lastMouse;
                if (_isBoxSelecting) {
                    _boxSelectionCurrent = evt.mousePosition;
                    Repaint();
                }
                else if (_isPanning) {
                    _pan += delta;
                    Repaint();
                }
                else if (_isDraggingNodes && (evt.mousePosition - _dragStartMouse).sqrMagnitude > 2f) {
                    foreach (var node in UpgradeTreeSelection.SelectedNodes) {
                        if (node == null) continue;
                        Undo.RecordObject(node, "Move Upgrade Node");
                        node.Position += delta / _zoom;
                        EditorUtility.SetDirty(node);
                    }
                    _dragMoved = true;
                    Repaint();
                }

                _lastMouse = evt.mousePosition;
                evt.Use();
            }

            if (evt.type == EventType.MouseDrag && evt.button == 1 && _phantomSource != null) {
                Repaint();
                evt.Use();
            }

            if (evt.type == EventType.MouseUp && evt.button == 0) {
                if (_isBoxSelecting) {
                    CompleteBoxSelection(evt.control || evt.command);
                }
                else if (_isDraggingNodes && _dragMoved) {
                    AssetDatabase.SaveAssets();
                }

                _isDraggingNodes = false;
                _isPanning = false;
                _isBoxSelecting = false;
                _dragNode = null;
                _dragMoved = false;
                evt.Use();
            }

            if (evt.type == EventType.MouseUp && evt.button == 1 && _phantomSource != null) {
                CompletePhantomLink(evt.mousePosition);
                _phantomSource = null;
                evt.Use();
            }
        }

        private void HandleKeyboard(Event evt) {
            if (evt.type != EventType.KeyDown) return;
            if (EditorGUIUtility.editingTextField || GUI.GetNameOfFocusedControl() == SearchControlName) return;

            if (evt.keyCode is KeyCode.Delete or KeyCode.Backspace) {
                DeleteSelectedNodes();
                evt.Use();
                return;
            }

            if (!evt.control && !evt.command) return;

            if (evt.keyCode == KeyCode.C) {
                _clipboard.Clear();
                _clipboard.AddRange(UpgradeTreeSelection.SelectedNodes.Where(node => node != null));
                evt.Use();
            }
            else if (evt.keyCode == KeyCode.V) {
                if (!UpgradeTreeAssetUtility.HasDuplicateIds(_nodes, out _)) {
                    var copies = UpgradeTreeAssetUtility.CopyNodes(_clipboard, CopyOffset, _nodes);
                    RefreshNodes();
                    UpgradeTreeSelection.SetSelection(copies);
                }
                evt.Use();
            }
        }

        private void CompletePhantomLink(Vector2 mousePosition) {
            var target = GetNodeAt(mousePosition);
            if (target == _phantomSource) return;

            if (target != null) {
                UpgradeTreeAssetUtility.ToggleChildLink(_phantomSource, target.Id);
            }
            else {
                var created = TryCreateNode(ScreenToWorld(mousePosition));
                if (created != null) {
                    UpgradeTreeAssetUtility.AppendChild(_phantomSource, created.Id);
                    UpgradeTreeSelection.SetSelection(created);
                }
            }

            RefreshNodes();
        }

        private UpgradeNodeDefinition TryCreateNode(Vector2 worldPosition) {
            if (UpgradeTreeAssetUtility.HasDuplicateIds(_nodes, out _)) return null;

            var id = UpgradeTreeAssetUtility.GenerateUniqueNodeId("upgrade_node", _nodes);
            var node = UpgradeTreeAssetUtility.CreateNode(id, worldPosition, _nodes);
            RefreshNodes();
            if (node != null) UpgradeTreeSelection.SetSelection(node);
            return node;
        }

        private UpgradeNodeDefinition GetNodeAt(Vector2 screenPosition) {
            for (var i = _nodes.Count - 1; i >= 0; i--) {
                var node = _nodes[i];
                if (node != null && GetNodeRect(node).Contains(screenPosition)) {
                    return node;
                }
            }

            return null;
        }

        private Rect GetNodeRect(UpgradeNodeDefinition node) {
            var size = GetScreenNodeSize();
            return new Rect(WorldToScreen(node.Position), new Vector2(size, size));
        }

        private Vector2 WorldToScreen(Vector2 world) {
            return world * _zoom + _pan;
        }

        private Vector2 ScreenToWorld(Vector2 screen) {
            return (screen - _pan) / _zoom;
        }

        private void RefreshNodes() {
            _nodes.Clear();
            _nodes.AddRange(UpgradeTreeAssetUtility.LoadNodes());
            RefreshSearchResults();
            Repaint();
        }

        private void RefreshSearchResults() {
            _searchResults.Clear();
            if (!string.IsNullOrWhiteSpace(_search)) {
                _searchResults.AddRange(_nodes.Where(node => node != null &&
                    ((node.Id != null && node.Id.ToLowerInvariant().Contains(_search.ToLowerInvariant())) ||
                     (node.Name != null && node.Name.ToLowerInvariant().Contains(_search.ToLowerInvariant())))));
            }

            _searchIndex = Mathf.Clamp(_searchIndex, 0, Mathf.Max(0, _searchResults.Count - 1));
            CenterSearchResult();
        }

        private void MoveSearch(int direction) {
            if (_searchResults.Count == 0) return;
            _searchIndex = (_searchIndex + direction + _searchResults.Count) % _searchResults.Count;
            CenterSearchResult();
        }

        private void CenterSearchResult() {
            if (_searchResults.Count == 0) return;
            var node = _searchResults[_searchIndex];
            if (node == null) return;
            _pan = new Vector2(position.width * 0.5f, position.height * 0.5f)
                   - (node.Position + Vector2.one * (_settings.NodeSize * 0.5f)) * _zoom;
            UpgradeTreeSelection.SetSelection(node);
            Repaint();
        }

        private void ResetActiveOperations() {
            if (_isDraggingNodes && _dragMoved) AssetDatabase.SaveAssets();
            _isDraggingNodes = false;
            _isPanning = false;
            _isBoxSelecting = false;
            _dragMoved = false;
            _dragNode = null;
            _phantomSource = null;
        }

        private void UpdateStyles() {
            _nodeLabelStyle ??= new GUIStyle(EditorStyles.whiteMiniLabel) {
                clipping = TextClipping.Clip,
                alignment = TextAnchor.MiddleCenter
            };
            _nodeLabelStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(_settings.IdTextSize * _zoom), 6, 48);

            _selectedLabelStyle ??= new GUIStyle(EditorStyles.boldLabel) {
                clipping = TextClipping.Overflow,
                alignment = TextAnchor.MiddleCenter
            };
            _selectedLabelStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(_settings.IdTextSize * _zoom), 6, 48);
            _selectedLabelStyle.normal.textColor = Color.white;
        }

        private float GetScreenNodeSize() {
            return _settings.NodeSize * _zoom;
        }

        private void ZoomAt(Vector2 screenPosition, float scrollDelta) {
            if (Mathf.Approximately(scrollDelta, 0f)) return;

            var worldUnderCursor = ScreenToWorld(screenPosition);
            var zoomFactor = 1f - scrollDelta * ZoomSensitivity;
            var nextZoom = Mathf.Clamp(_zoom * zoomFactor, MinZoom, MaxZoom);
            if (Mathf.Approximately(nextZoom, _zoom)) return;

            _zoom = nextZoom;
            _pan = screenPosition - worldUnderCursor * _zoom;
            Repaint();
        }

        private void CompleteBoxSelection(bool additive) {
            var selectionRect = GetNormalizedRect(_boxSelectionStart, _boxSelectionCurrent);
            var selected = additive
                ? UpgradeTreeSelection.SelectedNodes.Where(node => node != null).ToList()
                : new List<UpgradeNodeDefinition>();

            foreach (var node in _nodes) {
                if (node == null) continue;
                if (selectionRect.Overlaps(GetNodeRect(node)) && !selected.Contains(node)) {
                    selected.Add(node);
                }
            }

            UpgradeTreeSelection.SetSelection(selected);
        }

        private void DeleteSelectedNodes() {
            var selected = UpgradeTreeSelection.SelectedNodes.Where(node => node != null).ToList();
            if (selected.Count == 0) return;

            UpgradeTreeAssetUtility.DeleteNodes(selected, _nodes);
            UpgradeTreeSelection.SetSelection((UpgradeNodeDefinition)null);
            RefreshNodes();
        }

        private static Rect GetNormalizedRect(Vector2 start, Vector2 end) {
            var xMin = Mathf.Min(start.x, end.x);
            var xMax = Mathf.Max(start.x, end.x);
            var yMin = Mathf.Min(start.y, end.y);
            var yMax = Mathf.Max(start.y, end.y);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static void RevealNodeFolder(UpgradeNodeDefinition node) {
            var path = AssetDatabase.GetAssetPath(node);
            if (string.IsNullOrWhiteSpace(path)) return;

            var folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var folderAsset = string.IsNullOrWhiteSpace(folder) ? null : AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder);
            Selection.activeObject = folderAsset != null ? folderAsset : node;
            EditorGUIUtility.PingObject(Selection.activeObject);
        }
    }

    internal static class RectExtensions {
        public static Rect Shrink(this Rect rect, float amount) {
            return new Rect(rect.x + amount, rect.y + amount, rect.width - amount * 2f, rect.height - amount * 2f);
        }
    }
}
