using Authoring;
using UnityEditor;
using UnityEngine;

namespace SigningGame.Editor.Signatures {
    [CustomEditor(typeof(SignaturePresetDefinition))]
    public sealed class SignaturePresetDefinitionEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox(
                "Alignment values are validated and resolved snapshots reserved for future bounded alignment. " +
                "The current matcher uses normalized, index-wise corridor geometry without alignment search.",
                MessageType.Info);
            DrawPreview((SignaturePresetDefinition)target, GUILayoutUtility.GetRect(10f, 220f, GUILayout.ExpandWidth(true)));
        }

        private static void DrawPreview(SignaturePresetDefinition preset, Rect rect) {
            EditorGUI.DrawRect(rect, new Color(0.08f, 0.08f, 0.08f));
            Handles.BeginGUI();
            int strokeIndex = 0;
            foreach (SignatureVariantDefinition variant in preset.Variants) {
                if (variant == null) continue;
                foreach (SignatureTemplateStrokeDefinition stroke in variant.Strokes) {
                    if (stroke == null) continue;
                    Color strokeColor = Color.HSVToRGB((strokeIndex * 0.61803398875f) % 1f, 0.72f, 1f);
                    strokeColor.a = Mathf.Clamp01(0.35f + stroke.Importance * 0.3f);
                    SignatureCorridorNodeDefinition firstNode = null;
                    SignatureCorridorNodeDefinition lastNode = null;
                    SignatureCorridorNodeDefinition previousNode = null;
                    for (int i = 0; i < stroke.Nodes.Count; i++) {
                        SignatureCorridorNodeDefinition node = stroke.Nodes[i];
                        if (node == null) {
                            previousNode = null;
                            continue;
                        }
                        firstNode ??= node;
                        lastNode = node;
                        Vector2 point = Map(rect, node.Position);
                        Handles.color = strokeColor;
                        Handles.DrawWireDisc(point, Vector3.forward, node.Radius * Mathf.Min(rect.width, rect.height));
                        if (previousNode != null) Handles.DrawLine(Map(rect, previousNode.Position), point);
                        Color nodeColor = strokeColor;
                        nodeColor.a = Mathf.Clamp01(strokeColor.a * Mathf.Clamp01(0.35f + node.Importance * 0.65f));
                        Handles.color = nodeColor;
                        Handles.DrawSolidDisc(point, Vector3.forward, 2.5f);
                        previousNode = node;
                    }
                    if (firstNode != null) {
                        Vector2 start = Map(rect, firstNode.Position);
                        Vector2 end = Map(rect, lastNode.Position);
                        Handles.color = new Color(0.2f, 1f, 0.3f, 1f);
                        Handles.DrawSolidDisc(start, Vector3.forward, 5f);
                        Handles.Label(start + new Vector2(6f, -8f), $"S {stroke.Id} (importance {stroke.Importance:F2})");
                        Handles.color = new Color(1f, 0.25f, 0.2f, 1f);
                        Handles.DrawWireDisc(end, Vector3.forward, 6f);
                        Handles.Label(end + new Vector2(6f, 10f), "E");
                    }
                    strokeIndex++;
                }
            }
            Handles.EndGUI();
        }
        private static Vector2 Map(Rect rect, Vector2 p) => new(rect.x + p.x * rect.width, rect.y + (1f - p.y) * rect.height);
    }

    [CustomEditor(typeof(SignatureProcessingProfileDefinition))]
    public sealed class SignatureProcessingProfileDefinitionEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();
        }
    }
}
