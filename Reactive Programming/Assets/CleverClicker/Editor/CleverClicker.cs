#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace CleverSelection
{
    [InitializeOnLoad]
    public class CleverClicker
    {
        private static List<GameObject> m_OverlappingObjects = new List<GameObject>();
        private static Vector2 m_ClickPosition;
        private static bool m_ShowPopup = false;
        private static int m_HoveredIndex = -1;
        private static int m_KeyboardIndex = -1;
        private static GameObject m_LastHighlighted = null;
        private static readonly Color m_HighlightColor = new Color(1f, 0.8f, 0.2f, 0.3f);

        private static Color m_CustomHighlightColor = new Color(1f, 0.8f, 0.2f, 1f);

        private static Vector2 m_ScrollPosition = Vector2.zero;
        private static int m_ControlID = 0;
        private static double m_PopupClosedTime = 0;
        private const double BLOCK_EVENTS_DURATION = 0.1;

        private static EventModifiers m_ModifierKey = EventModifiers.Alt;
        private static bool m_FilterTerrain = true;
        private static float m_MaxDistance = 100000f;
        private static int m_ExcludedLayerMask = 0;
        private static bool m_FocusOnSelect = false;

        private static bool m_UseControl = true;
        private static bool m_UseAlt = false;
        private static bool m_UseShift = true;
        private static bool m_UseCommand = false;

        private const string USE_CONTROL_PREF = "CleverClicker_UseControl";
        private const string USE_ALT_PREF = "CleverClicker_UseAlt";
        private const string USE_SHIFT_PREF = "CleverClicker_UseShift";
        private const string USE_COMMAND_PREF = "CleverClicker_UseCommand";
        private const string EXCLUDED_LAYERS_PREF = "CleverClicker_ExcludedLayers";
        private const string FOCUS_ON_SELECT_PREF = "CleverClicker_FocusOnSelect";
        private const string HIGHLIGHT_COLOR_PREF = "CleverClicker_HighlightColor";

        static CleverClicker()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            LoadSettings();
        }

        private static void LoadSettings()
        {
            m_UseControl = EditorPrefs.GetBool(USE_CONTROL_PREF, true);
            m_UseAlt = EditorPrefs.GetBool(USE_ALT_PREF, false);
            m_UseShift = EditorPrefs.GetBool(USE_SHIFT_PREF, true);
            m_UseCommand = EditorPrefs.GetBool(USE_COMMAND_PREF, false);
            m_ExcludedLayerMask = EditorPrefs.GetInt(EXCLUDED_LAYERS_PREF, 0);
            m_FocusOnSelect = EditorPrefs.GetBool(FOCUS_ON_SELECT_PREF, false);

            string colorHex = EditorPrefs.GetString(HIGHLIGHT_COLOR_PREF, "#FFCC33FF");
            if (ColorUtility.TryParseHtmlString(colorHex, out Color loadedColor))
            {
                m_CustomHighlightColor = loadedColor;
            }

            if (!m_UseControl && !m_UseAlt && !m_UseShift && !m_UseCommand)
            {
                m_UseControl = true;
                m_UseShift = true;
                EditorPrefs.SetBool(USE_CONTROL_PREF, true);
                EditorPrefs.SetBool(USE_SHIFT_PREF, true);
            }
        }



        public static void SetExcludedLayerMask(int mask)
        {
            EditorPrefs.SetInt(EXCLUDED_LAYERS_PREF, mask);
            m_ExcludedLayerMask = mask;
        }

        public static int GetExcludedLayerMask()
        {
            return EditorPrefs.GetInt(EXCLUDED_LAYERS_PREF, 0);
        }

        public static void SetFocusOnSelect(bool focus)
        {
            EditorPrefs.SetBool(FOCUS_ON_SELECT_PREF, focus);
            m_FocusOnSelect = focus;
        }

        public static bool GetFocusOnSelect()
        {
            return EditorPrefs.GetBool(FOCUS_ON_SELECT_PREF, false);
        }

        public static void SetHighlightColor(Color color)
        {
            m_CustomHighlightColor = color;
            EditorPrefs.SetString(HIGHLIGHT_COLOR_PREF, "#" + ColorUtility.ToHtmlStringRGBA(color));
        }

        public static Color GetHighlightColor()
        {
            string colorHex = EditorPrefs.GetString(HIGHLIGHT_COLOR_PREF, "#FFCC33FF");
            if (ColorUtility.TryParseHtmlString(colorHex, out Color loadedColor))
            {
                return loadedColor;
            }
            return new Color(1f, 0.8f, 0.2f, 1f);
        }

        public static void SetUseControl(bool value)
        {
            m_UseControl = value;
            EditorPrefs.SetBool(USE_CONTROL_PREF, value);
            ValidateModifiers();
        }

        public static void SetUseAlt(bool value)
        {
            m_UseAlt = value;
            EditorPrefs.SetBool(USE_ALT_PREF, value);
            ValidateModifiers();
        }

        public static void SetUseShift(bool value)
        {
            m_UseShift = value;
            EditorPrefs.SetBool(USE_SHIFT_PREF, value);
            ValidateModifiers();
        }

        public static void SetUseCommand(bool value)
        {
            m_UseCommand = value;
            EditorPrefs.SetBool(USE_COMMAND_PREF, value);
            ValidateModifiers();
        }

        public static bool GetUseControl() => m_UseControl;
        public static bool GetUseAlt() => m_UseAlt;
        public static bool GetUseShift() => m_UseShift;
        public static bool GetUseCommand() => m_UseCommand;

        private static void ValidateModifiers()
        {
            if (!m_UseControl && !m_UseAlt && !m_UseShift && !m_UseCommand)
            {
                m_UseControl = true;
                EditorPrefs.SetBool(USE_CONTROL_PREF, true);
            }
        }

        public static string GetModifierString()
        {
            List<string> modifiers = new List<string>();
            if (m_UseControl) modifiers.Add("Ctrl");
            if (m_UseAlt) modifiers.Add("Alt");
            if (m_UseShift) modifiers.Add("Shift");
            if (m_UseCommand) modifiers.Add("Cmd");
            return string.Join(" + ", modifiers) + " + Click";
        }

        private static bool CheckModifiers(Event e)
        {
            bool controlMatch = m_UseControl == e.control;
            bool altMatch = m_UseAlt == e.alt;
            bool shiftMatch = m_UseShift == e.shift;
            bool commandMatch = m_UseCommand == e.command;

            return controlMatch && altMatch && shiftMatch && commandMatch;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            Event e = Event.current;

            if (EditorApplication.timeSinceStartup - m_PopupClosedTime < BLOCK_EVENTS_DURATION)
            {
                if (e.type == EventType.MouseDown || e.type == EventType.MouseUp)
                {
                    e.Use();
                    return;
                }
            }
            if (m_ShowPopup && EditorWindow.focusedWindow != sceneView)
            {
                ClosePopup();
                sceneView.Repaint();
                return;
            }

            if (e.type == EventType.MouseDown &&
                e.button == 0 &&
                CheckModifiers(e))
            {
                if (m_ShowPopup && IsMouseInPopup(e.mousePosition))
                {
                }
                else
                {
                    HandleClick(e);
                    e.Use();
                }
            }

            if (m_ShowPopup)
            {
                m_ControlID = GUIUtility.GetControlID(FocusType.Passive);
                GUIUtility.hotControl = m_ControlID;

                int highlightIndex = m_HoveredIndex >= 0 ? m_HoveredIndex : m_KeyboardIndex;

                if (highlightIndex >= 0 && highlightIndex < m_OverlappingObjects.Count)
                {
                    DrawObjectHighlight(m_OverlappingObjects[highlightIndex]);
                }

                DrawPopup(sceneView);

                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    if (!IsMouseInPopup(e.mousePosition))
                    {
                        ClosePopup();
                        e.Use();
                    }
                }
                else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
                {
                    ClosePopup();
                    e.Use();
                }
                else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.DownArrow)
                {
                    m_KeyboardIndex = Mathf.Min(m_KeyboardIndex + 1, m_OverlappingObjects.Count - 1);
                    m_HoveredIndex = m_KeyboardIndex;
                    ScrollToIndex(m_KeyboardIndex);
                    e.Use();
                }
                else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.UpArrow)
                {
                    m_KeyboardIndex = Mathf.Max(m_KeyboardIndex - 1, 0);
                    m_HoveredIndex = m_KeyboardIndex;
                    ScrollToIndex(m_KeyboardIndex);
                    e.Use();
                }
                else if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
                {
                    if (m_KeyboardIndex >= 0 && m_KeyboardIndex < m_OverlappingObjects.Count)
                    {
                        Selection.activeGameObject = m_OverlappingObjects[m_KeyboardIndex];
                        if (m_FocusOnSelect)
                        {
                            SceneView.lastActiveSceneView.FrameSelected();
                        }
                        ClosePopup();
                        e.Use();
                    }
                }

                sceneView.Repaint();
            }
        }

        private static void ScrollToIndex(int index)
        {
            float itemHeight = 24f;
            float maxHeight = 400f;
            float headerHeight = 28f;
            float padding = 8f;
            float contentHeight = m_OverlappingObjects.Count * itemHeight;
            float viewHeight = Mathf.Min(contentHeight + headerHeight + padding * 2, maxHeight) - headerHeight - padding * 2;

            float itemTop = index * itemHeight;
            float itemBottom = itemTop + itemHeight;

            if (itemTop < m_ScrollPosition.y)
            {
                m_ScrollPosition.y = itemTop;
            }
            else if (itemBottom > m_ScrollPosition.y + viewHeight)
            {
                m_ScrollPosition.y = itemBottom - viewHeight;
            }
        }

        private static void HandleClick(Event e)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            m_ClickPosition = e.mousePosition;

            m_OverlappingObjects.Clear();

            RaycastHit[] hits = Physics.RaycastAll(ray, m_MaxDistance);

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider != null && hit.collider.gameObject != null)
                {
                    GameObject obj = hit.collider.gameObject;

                    if (m_FilterTerrain && obj.GetComponent<Terrain>() != null)
                        continue;

                    if (((1 << obj.layer) & m_ExcludedLayerMask) != 0)
                        continue;

                    if (!m_OverlappingObjects.Contains(obj))
                    {
                        m_OverlappingObjects.Add(obj);
                    }
                }
            }

            RaycastHit2D[] hits2D = Physics2D.GetRayIntersectionAll(ray, m_MaxDistance);

            foreach (RaycastHit2D hit in hits2D)
            {
                if (hit.collider != null && hit.collider.gameObject != null)
                {
                    GameObject obj = hit.collider.gameObject;

                    if (((1 << obj.layer) & m_ExcludedLayerMask) != 0)
                        continue;

                    if (!m_OverlappingObjects.Contains(obj))
                    {
                        m_OverlappingObjects.Add(obj);
                    }
                }
            }

            List<GameObject> spriteObjects = GetSpritesAtScreenPoint(e.mousePosition);
            foreach (GameObject obj in spriteObjects)
            {
                if (!m_OverlappingObjects.Contains(obj))
                {
                    if (((1 << obj.layer) & m_ExcludedLayerMask) == 0)
                    {
                        m_OverlappingObjects.Add(obj);
                    }
                }
            }
            List<GameObject> ignoredObjects = new List<GameObject>();
            int maxPicks = 50;
            for (int i = 0; i < maxPicks; i++)
            {
                GameObject handleObj = HandleUtility.PickGameObject(e.mousePosition, false, ignoredObjects.ToArray());
                if (handleObj == null)
                    break;

                ignoredObjects.Add(handleObj);

                if (!m_OverlappingObjects.Contains(handleObj))
                {
                    if (((1 << handleObj.layer) & m_ExcludedLayerMask) == 0)
                    {
                        m_OverlappingObjects.Add(handleObj);
                    }
                }
            }

            List<GameObject> screenObjects = GetObjectsAtScreenPoint(e.mousePosition);
            foreach (GameObject obj in screenObjects)
            {
                if (!m_OverlappingObjects.Contains(obj))
                {
                    if (((1 << obj.layer) & m_ExcludedLayerMask) == 0)
                    {
                        m_OverlappingObjects.Add(obj);
                    }
                }
            }

            List<GameObject> uiObjects = GetUIObjectsAtScreenPoint(e.mousePosition);
            foreach (GameObject obj in uiObjects)
            {
                if (!m_OverlappingObjects.Contains(obj))
                {
                    if (((1 << obj.layer) & m_ExcludedLayerMask) == 0)
                    {
                        m_OverlappingObjects.Add(obj);
                    }
                }
            }

            Camera sceneCam = SceneView.lastActiveSceneView.camera;
            m_OverlappingObjects = m_OverlappingObjects
                .OrderBy(obj => Vector3.Distance(sceneCam.transform.position, obj.transform.position))
                .ToList();

            if (m_OverlappingObjects.Count > 0)
            {
                m_ShowPopup = true;
                m_HoveredIndex = -1;
                m_KeyboardIndex = 0;
                m_ScrollPosition = Vector2.zero;
            }
        }

        private static List<GameObject> GetObjectsAtScreenPoint(Vector2 screenPoint)
        {
            List<GameObject> objects = new List<GameObject>();

            GameObject[] allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            Camera sceneCam = SceneView.lastActiveSceneView.camera;

            foreach (GameObject obj in allObjects)
            {
                Vector3 screenPos = sceneCam.WorldToScreenPoint(obj.transform.position);

                if (screenPos.z > 0)
                {
                    Vector2 sceneViewScreenPos = HandleUtility.WorldToGUIPoint(obj.transform.position);
                    float distance = Vector2.Distance(sceneViewScreenPos, screenPoint);

                    if (distance < 20f)
                    {
                        objects.Add(obj);
                    }
                }
            }

            return objects;
        }

        private static List<GameObject> GetSpritesAtScreenPoint(Vector2 guiPoint)
        {
            List<GameObject> spriteObjects = new List<GameObject>();
            Camera sceneCam = SceneView.lastActiveSceneView.camera;

            Ray ray = HandleUtility.GUIPointToWorldRay(guiPoint);

            SpriteRenderer[] spriteRenderers = GameObject.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);

            foreach (SpriteRenderer sr in spriteRenderers)
            {
                if (sr == null || sr.sprite == null) continue;
                if (!sr.gameObject.activeInHierarchy) continue;

                Bounds bounds = sr.bounds;

                Plane spritePlane = new Plane(-sceneCam.transform.forward, sr.transform.position);

                float enter;
                if (spritePlane.Raycast(ray, out enter))
                {
                    Vector3 hitPoint = ray.GetPoint(enter);

                    if (bounds.Contains(hitPoint))
                    {
                        if (!spriteObjects.Contains(sr.gameObject))
                        {
                            spriteObjects.Add(sr.gameObject);
                        }
                    }
                }

                if (bounds.IntersectRay(ray))
                {
                    if (!spriteObjects.Contains(sr.gameObject))
                    {
                        spriteObjects.Add(sr.gameObject);
                    }
                }
            }

            return spriteObjects;
        }

        private static List<GameObject> GetUIObjectsAtScreenPoint(Vector2 guiPoint)
        {
            List<GameObject> uiObjects = new List<GameObject>();
            Camera sceneCam = SceneView.lastActiveSceneView.camera;

            Canvas[] canvases = GameObject.FindObjectsByType<Canvas>(FindObjectsSortMode.None);

            foreach (Canvas canvas in canvases)
            {
                if (canvas == null) continue;

                RectTransform[] rectTransforms = canvas.GetComponentsInChildren<RectTransform>();

                foreach (RectTransform rectTransform in rectTransforms)
                {
                    if (rectTransform == null || rectTransform.gameObject == canvas.gameObject) continue;

                    Vector2 screenPoint = HandleUtility.GUIPointToScreenPixelCoordinate(guiPoint);

                    if (RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPoint, sceneCam))
                    {
                        if (!uiObjects.Contains(rectTransform.gameObject))
                        {
                            uiObjects.Add(rectTransform.gameObject);
                        }
                    }

                    if (canvas.renderMode == RenderMode.WorldSpace)
                    {
                        Vector3 worldPoint = rectTransform.position;
                        Vector2 sceneViewPoint = HandleUtility.WorldToGUIPoint(worldPoint);
                        float distance = Vector2.Distance(sceneViewPoint, guiPoint);

                        if (distance < 30f)
                        {
                            if (!uiObjects.Contains(rectTransform.gameObject))
                            {
                                uiObjects.Add(rectTransform.gameObject);
                            }
                        }
                    }
                }
            }

            return uiObjects;
        }

        private static void DrawPopup(SceneView sceneView)
        {
            Handles.BeginGUI();

            float itemHeight = 24f;
            float width = 250f;
            float headerHeight = 28f;
            float padding = 8f;
            float contentHeight = m_OverlappingObjects.Count * itemHeight;
            float maxHeight = Mathf.Min(400f, sceneView.position.height - 40f);
            float actualHeight = Mathf.Min(contentHeight + headerHeight + padding * 2, maxHeight);

            float popupY = m_ClickPosition.y;

            if (popupY + actualHeight > sceneView.position.height - 20f)
            {
                popupY = m_ClickPosition.y - actualHeight + 20f;
            }

            if (m_OverlappingObjects.Count > 5)
            {
                float offsetRatio = Mathf.Clamp01((m_OverlappingObjects.Count - 5) / 15f);
                popupY -= offsetRatio * (actualHeight * 0.3f);
            }

            Rect popupRect = new Rect(
                m_ClickPosition.x + 10f,
                popupY,
                width,
                actualHeight
            );

            if (popupRect.xMax > sceneView.position.width)
                popupRect.x = sceneView.position.width - popupRect.width - 10f;
            if (popupRect.yMax > sceneView.position.height - 10f)
                popupRect.y = sceneView.position.height - popupRect.height - 10f;
            if (popupRect.y < 10f)
                popupRect.y = 10f;

            EditorGUI.DrawRect(popupRect, new Color(0.2f, 0.2f, 0.2f, 0.95f));

            Handles.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            Handles.DrawSolidRectangleWithOutline(popupRect, Color.clear, new Color(0.1f, 0.1f, 0.1f, 1f));

            Rect titleBgRect = new Rect(popupRect.x, popupRect.y, popupRect.width, headerHeight);
            EditorGUI.DrawRect(titleBgRect, new Color(0.15f, 0.15f, 0.15f, 1f));

            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                titleBgRect.Contains(Event.current.mousePosition))
            {
                Selection.objects = m_OverlappingObjects.Where(obj => obj != null).ToArray();
                ClosePopup();
                Event.current.Use();
                Handles.EndGUI();
                return;
            }

            if (titleBgRect.Contains(Event.current.mousePosition))
            {
                EditorGUI.DrawRect(titleBgRect, new Color(0.3f, 0.5f, 0.8f, 0.3f));
                EditorGUIUtility.AddCursorRect(titleBgRect, MouseCursor.Link);
            }

            Rect titleRect = new Rect(popupRect.x + padding, popupRect.y + 5f, popupRect.width - padding * 2, 18f);
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            GUI.Label(titleRect, $"Select All Objects ({m_OverlappingObjects.Count})", titleStyle);

            Rect scrollRect = new Rect(popupRect.x + padding, popupRect.y + headerHeight + padding, popupRect.width - padding * 2, popupRect.height - headerHeight - padding * 2);

            float totalContentHeight = m_OverlappingObjects.Count * itemHeight;
            Rect contentRect = new Rect(0, 0, scrollRect.width - 16f, totalContentHeight);

            Vector2 originalMousePos = Event.current.mousePosition;
            bool mouseInScrollArea = scrollRect.Contains(originalMousePos);

            m_ScrollPosition = GUI.BeginScrollView(scrollRect, m_ScrollPosition, contentRect);

            m_HoveredIndex = -1;
            for (int i = 0; i < m_OverlappingObjects.Count; i++)
            {
                GameObject obj = m_OverlappingObjects[i];
                if (obj == null) continue;

                Rect itemRect = new Rect(
                    0,
                    i * itemHeight,
                    contentRect.width,
                    itemHeight
                );

                bool isHovered = mouseInScrollArea && itemRect.Contains(Event.current.mousePosition);
                bool isKeyboardSelected = (i == m_KeyboardIndex);

                if (isHovered)
                {
                    m_HoveredIndex = i;
                    m_KeyboardIndex = i;
                }

                if (isHovered || isKeyboardSelected)
                {
                    EditorGUI.DrawRect(itemRect, new Color(0.3f, 0.5f, 0.8f, 0.3f));
                }

                Texture2D icon = AssetPreview.GetMiniThumbnail(obj);

                Rect iconRect = new Rect(itemRect.x + 2f, itemRect.y + 3f, 18f, 18f);
                if (icon != null)
                {
                    GUI.DrawTexture(iconRect, icon);
                }

                Rect labelRect = new Rect(itemRect.x + 24f, itemRect.y + 3f, itemRect.width - 26f, itemHeight);
                string label = obj.name;

                Component[] components = obj.GetComponents<Component>();
                if (components.Length > 1)
                {
                    string mainComponent = components.FirstOrDefault(c => !(c is Transform))?.GetType().Name ?? "";
                    if (!string.IsNullOrEmpty(mainComponent))
                    {
                        label += $"  <color=#AAAAAA>[{mainComponent}]</color>";
                    }
                }

                GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
                labelStyle.richText = true;
                labelStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1f);
                GUI.Label(labelRect, label, labelStyle);

                if (isHovered && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    Selection.activeGameObject = obj;
                    if (m_FocusOnSelect)
                    {
                        SceneView.lastActiveSceneView.FrameSelected();
                    }
                    ClosePopup();
                    Event.current.Use();
                    GUI.EndScrollView();
                    Handles.EndGUI();
                    return;
                }
            }

            GUI.EndScrollView();

            Handles.EndGUI();
        }

        private static bool IsMouseInPopup(Vector2 mousePosition)
        {
            float itemHeight = 24f;
            float width = 250f;
            float maxHeight = 400f;
            float headerHeight = 28f;
            float padding = 8f;
            float contentHeight = m_OverlappingObjects.Count * itemHeight;
            float actualHeight = Mathf.Min(contentHeight + headerHeight + padding * 2, maxHeight);

            float popupY = m_ClickPosition.y;

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                if (popupY + actualHeight > sceneView.position.height - 20f)
                {
                    popupY = m_ClickPosition.y - actualHeight + 20f;
                }

                if (m_OverlappingObjects.Count > 5)
                {
                    float offsetRatio = Mathf.Clamp01((m_OverlappingObjects.Count - 5) / 15f);
                    popupY -= offsetRatio * (actualHeight * 0.3f);
                }
            }

            Rect popupRect = new Rect(
                m_ClickPosition.x + 10f,
                popupY,
                width,
                actualHeight
            );

            return popupRect.Contains(mousePosition);
        }

        private static void DrawObjectHighlight(GameObject obj)
        {
            if (obj == null) return;

            if (m_LastHighlighted != obj)
            {
                m_LastHighlighted = obj;
            }

            Renderer renderer = obj.GetComponent<Renderer>();
            RectTransform rectTransform = obj.GetComponent<RectTransform>();

            Bounds bounds;
            bool hasBounds = false;

            if (renderer != null)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else if (rectTransform != null)
            {
                Vector3[] corners = new Vector3[4];
                rectTransform.GetWorldCorners(corners);
                bounds = new Bounds(corners[0], Vector3.zero);
                for (int i = 1; i < 4; i++)
                {
                    bounds.Encapsulate(corners[i]);
                }
                hasBounds = true;
            }
            else
            {
                bounds = new Bounds();
            }

            if (hasBounds)
            {
                Color solidColor = m_CustomHighlightColor;
                solidColor.a = 1f;
                Handles.color = solidColor;
                Camera sceneCam = SceneView.lastActiveSceneView.camera;
                float distance = Vector3.Distance(sceneCam.transform.position, bounds.center);
                float largestDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                float apparentSize = largestDimension / Mathf.Max(distance, 0.001f);
                if (apparentSize > 1f)
                {
                    int targetDivisions = apparentSize > 2f ? 6 : 4;
                    DrawSubdividedWireCube(bounds.center, bounds.size, targetDivisions);
                }
                else
                {
                    DrawWireCube(bounds.center, bounds.size);
                }
                Color glowColor = m_CustomHighlightColor;
                glowColor.a = 0.3f;
                Handles.color = glowColor;
                DrawWireCube(bounds.center, bounds.size * 1.01f);
                float crosshairSize = distance * 0.01f;
                crosshairSize = Mathf.Clamp(crosshairSize, 0.1f, 20f);
                DrawCrosshair(bounds.center, crosshairSize);
            }
            else
            {
                Camera sceneCam = SceneView.lastActiveSceneView.camera;
                float distance = Vector3.Distance(sceneCam.transform.position, obj.transform.position);
                float crosshairSize = distance * 0.01f;
                crosshairSize = Mathf.Clamp(crosshairSize, 0.1f, 20f);
                DrawCrosshair(obj.transform.position, crosshairSize);
            }
        }

        private static void DrawCrosshair(Vector3 worldPosition, float size)
        {
            Color solidColor = m_CustomHighlightColor;
            solidColor.a = 1f;
            Handles.color = solidColor;
            Handles.DrawLine(worldPosition - Vector3.right * size, worldPosition + Vector3.right * size);
            Handles.DrawLine(worldPosition - Vector3.up * size, worldPosition + Vector3.up * size);
            Handles.DrawLine(worldPosition - Vector3.forward * size, worldPosition + Vector3.forward * size);
            Color glowColor = m_CustomHighlightColor;
            glowColor.a = 0.3f;
            Handles.color = glowColor;
            Handles.SphereHandleCap(0, worldPosition, Quaternion.identity, size * 0.3f, EventType.Repaint);
        }

        private static void DrawWireCube(Vector3 center, Vector3 size)
        {
            Vector3 halfSize = size * 0.5f;

            Vector3[] points = new Vector3[8];
            points[0] = center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z);
            points[1] = center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z);
            points[2] = center + new Vector3(halfSize.x, -halfSize.y, halfSize.z);
            points[3] = center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z);
            points[4] = center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z);
            points[5] = center + new Vector3(halfSize.x, halfSize.y, -halfSize.z);
            points[6] = center + new Vector3(halfSize.x, halfSize.y, halfSize.z);
            points[7] = center + new Vector3(-halfSize.x, halfSize.y, halfSize.z);

            Handles.DrawLine(points[0], points[1]);
            Handles.DrawLine(points[1], points[2]);
            Handles.DrawLine(points[2], points[3]);
            Handles.DrawLine(points[3], points[0]);

            Handles.DrawLine(points[4], points[5]);
            Handles.DrawLine(points[5], points[6]);
            Handles.DrawLine(points[6], points[7]);
            Handles.DrawLine(points[7], points[4]);

            Handles.DrawLine(points[0], points[4]);
            Handles.DrawLine(points[1], points[5]);
            Handles.DrawLine(points[2], points[6]);
            Handles.DrawLine(points[3], points[7]);
        }

        private static void DrawSubdividedWireCube(Vector3 center, Vector3 size, int targetDivisions)
        {
            DrawWireCube(center, size);

            Vector3 halfSize = size * 0.5f;
            Vector3 min = center - halfSize;
            Vector3 max = center + halfSize;
            int divisionsX = targetDivisions;
            int divisionsY = targetDivisions;
            int divisionsZ = targetDivisions;

            float spacingX = size.x / divisionsX;
            float spacingY = size.y / divisionsY;
            float spacingZ = size.z / divisionsZ;
            for (int i = 1; i < divisionsX; i++)
            {
                float x = min.x + i * spacingX;
                Handles.DrawLine(new Vector3(x, max.y, min.z), new Vector3(x, max.y, max.z));
                Handles.DrawLine(new Vector3(x, min.y, min.z), new Vector3(x, min.y, max.z));
            }
            for (int i = 1; i < divisionsZ; i++)
            {
                float z = min.z + i * spacingZ;
                Handles.DrawLine(new Vector3(min.x, max.y, z), new Vector3(max.x, max.y, z));
                Handles.DrawLine(new Vector3(min.x, min.y, z), new Vector3(max.x, min.y, z));
            }
            for (int i = 1; i < divisionsX; i++)
            {
                float x = min.x + i * spacingX;
                Handles.DrawLine(new Vector3(x, min.y, max.z), new Vector3(x, max.y, max.z));
                Handles.DrawLine(new Vector3(x, min.y, min.z), new Vector3(x, max.y, min.z));
            }
            for (int i = 1; i < divisionsY; i++)
            {
                float y = min.y + i * spacingY;
                Handles.DrawLine(new Vector3(min.x, y, max.z), new Vector3(max.x, y, max.z));
                Handles.DrawLine(new Vector3(min.x, y, min.z), new Vector3(max.x, y, min.z));
            }
            for (int i = 1; i < divisionsY; i++)
            {
                float y = min.y + i * spacingY;
                Handles.DrawLine(new Vector3(min.x, y, min.z), new Vector3(min.x, y, max.z));
                Handles.DrawLine(new Vector3(max.x, y, min.z), new Vector3(max.x, y, max.z));
            }
            for (int i = 1; i < divisionsZ; i++)
            {
                float z = min.z + i * spacingZ;
                Handles.DrawLine(new Vector3(min.x, min.y, z), new Vector3(min.x, max.y, z));
                Handles.DrawLine(new Vector3(max.x, min.y, z), new Vector3(max.x, max.y, z));
            }
        }

        private static void ClosePopup()
        {
            m_ShowPopup = false;
            m_HoveredIndex = -1;
            m_KeyboardIndex = -1;
            m_LastHighlighted = null;
            m_ScrollPosition = Vector2.zero;
            m_OverlappingObjects.Clear();
            m_PopupClosedTime = EditorApplication.timeSinceStartup;

            if (GUIUtility.hotControl == m_ControlID)
            {
                GUIUtility.hotControl = 0;
            }
        }

        [MenuItem("Tools/Clever Clicker/Settings")]
        private static void OpenSettings()
        {
            CleverClickerSettings.ShowWindow();
        }
    }

    public class CleverClickerSettings : EditorWindow
    {
        private static string[] m_SelectionBehaviorOptions = new string[] { "Select Only", "Select and Focus" };

        public static void ShowWindow()
        {
            CleverClickerSettings window = GetWindow<CleverClickerSettings>("Clever Clicker");
            window.minSize = new Vector2(350, 420);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("Clever Clicker Settings", EditorStyles.boldLabel);
            GUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "Hold the modifier key combination and click in the Scene view to show all objects at that position.",
                MessageType.Info
            );

            GUILayout.Space(10);
            GUILayout.Label("Modifier Keys:", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            bool useControl = EditorGUILayout.ToggleLeft("Control (Ctrl)", CleverClicker.GetUseControl());
            bool useAlt = EditorGUILayout.ToggleLeft("Alt", CleverClicker.GetUseAlt());
            bool useShift = EditorGUILayout.ToggleLeft("Shift", CleverClicker.GetUseShift());
            bool useCommand = EditorGUILayout.ToggleLeft("Command (Cmd)", CleverClicker.GetUseCommand());

            if (EditorGUI.EndChangeCheck())
            {
                CleverClicker.SetUseControl(useControl);
                CleverClicker.SetUseAlt(useAlt);
                CleverClicker.SetUseShift(useShift);
                CleverClicker.SetUseCommand(useCommand);
            }

            GUILayout.Space(5);
            GUIStyle previewStyle = new GUIStyle(EditorStyles.helpBox);
            previewStyle.normal.textColor = new Color(0.7f, 0.9f, 1f);
            previewStyle.fontStyle = FontStyle.Bold;
            previewStyle.padding = new RectOffset(10, 10, 5, 5);

            GUILayout.Label($"Current: {CleverClicker.GetModifierString()}", previewStyle);
            if (useAlt && !useControl && !useShift && !useCommand)
            {
                EditorGUILayout.HelpBox(
                    "Alt may interfere with camera rotation (Alt + Drag)",
                    MessageType.Warning
                );
            }

            if (useShift && !useControl && !useAlt && !useCommand)
            {
                EditorGUILayout.HelpBox(
                    "Shift conflicts with multi-select (Shift + Click adds to selection)",
                    MessageType.Warning
                );
            }

            if (useControl && !useAlt && !useShift && !useCommand)
            {
                EditorGUILayout.HelpBox(
                    "Ctrl conflicts with multi-select (Ctrl + Click removes from selection)",
                    MessageType.Warning
                );
            }

            GUILayout.Space(10);
            GUILayout.Label("Selection Behavior:", EditorStyles.label);

            bool currentFocus = CleverClicker.GetFocusOnSelect();
            int currentBehavior = currentFocus ? 1 : 0;
            int newBehavior = EditorGUILayout.Popup(currentBehavior, m_SelectionBehaviorOptions, GUILayout.Width(150));

            if (newBehavior != currentBehavior)
            {
                CleverClicker.SetFocusOnSelect(newBehavior == 1);
            }

            GUILayout.Space(10);
            GUILayout.Label("Highlight Color:", EditorStyles.label);

            Color currentColor = CleverClicker.GetHighlightColor();
            Color newColor = EditorGUILayout.ColorField(currentColor, GUILayout.Width(150));

            if (newColor != currentColor)
            {
                CleverClicker.SetHighlightColor(newColor);
            }

            GUILayout.Space(15);
            GUILayout.Label("Excluded Layers:", EditorStyles.label);
            EditorGUILayout.HelpBox(
                "Objects on these layers will be ignored.",
                MessageType.None
            );

            int currentMask = CleverClicker.GetExcludedLayerMask();
            int newMask = EditorGUILayout.MaskField(currentMask, GetLayerNames(), GUILayout.Width(150));

            if (newMask != currentMask)
            {
                CleverClicker.SetExcludedLayerMask(newMask);
            }

            GUILayout.Space(20);
            GUILayout.Label("Usage:", EditorStyles.boldLabel);
            GUILayout.Label($"• Hold {CleverClicker.GetModifierString()} in Scene view");
            GUILayout.Label("• Hover over items to highlight them");
            GUILayout.Label("• Use ↑↓ arrow keys to navigate");
            GUILayout.Label("• Press Enter to select");
            GUILayout.Label("• Click an item to select it");
            GUILayout.Label("• Click header to select all");
            GUILayout.Label("• Press Escape or click outside to close");
        }

        private string[] GetLayerNames()
        {
            string[] layers = new string[32];
            for (int i = 0; i < 32; i++)
            {
                string layerName = LayerMask.LayerToName(i);
                layers[i] = string.IsNullOrEmpty(layerName) ? $"Layer {i}" : layerName;
            }
            return layers;
        }
    }
}
#endif