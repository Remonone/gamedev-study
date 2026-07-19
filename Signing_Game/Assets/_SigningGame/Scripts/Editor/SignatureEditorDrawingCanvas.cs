using System;
using System.Collections.Generic;
using Data.Input;
using UnityEditor;
using UnityEngine;

namespace SigningGame.Editor.Signatures {
    public sealed class SignatureEditorDrawingCanvas {
        private readonly List<List<SignatureInputPoint>> _strokes = new();
        private List<SignatureInputPoint> _activeStroke;
        public IReadOnlyList<List<SignatureInputPoint>> Strokes => _strokes;
        public int Revision { get; private set; }

        public void DrawAndCapture(Rect rect) {
            EditorGUI.DrawRect(rect, new Color(0.08f, 0.08f, 0.08f));
            GUI.Box(rect, GUIContent.none);
            Event current = Event.current;
            if (current.button == 0 && rect.Contains(current.mousePosition)) {
                if (current.type == EventType.MouseDown) {
                    _activeStroke = new List<SignatureInputPoint>(); _strokes.Add(_activeStroke);
                    IncrementRevision();
                    AddPoint(rect, current.mousePosition); current.Use();
                } else if (current.type == EventType.MouseDrag && _activeStroke != null) {
                    AddPoint(rect, current.mousePosition); current.Use();
                } else if (current.type == EventType.MouseUp && _activeStroke != null) {
                    AddPoint(rect, current.mousePosition); _activeStroke = null; current.Use();
                }
            }
            Handles.BeginGUI();
            Handles.color = Color.white;
            foreach (List<SignatureInputPoint> stroke in _strokes) for (int i = 1; i < stroke.Count; i++)
                Handles.DrawLine(ToCanvas(rect, stroke[i - 1].Position), ToCanvas(rect, stroke[i].Position));
            Handles.EndGUI();
        }

        public SignatureAttempt ToAttempt() {
            var strokes = new List<SignatureStrokeAttempt>(_strokes.Count);
            float first = float.PositiveInfinity, last = float.NegativeInfinity;
            foreach (List<SignatureInputPoint> stroke in _strokes) {
                strokes.Add(new SignatureStrokeAttempt(stroke.ToArray()));
                foreach (SignatureInputPoint point in stroke) { first = Mathf.Min(first, point.Time); last = Mathf.Max(last, point.Time); }
            }
            return new SignatureAttempt(strokes, float.IsInfinity(first) ? 0f : Mathf.Max(0f, last - first));
        }

        public void Clear() { _strokes.Clear(); _activeStroke = null; IncrementRevision(); }
        private void AddPoint(Rect rect, Vector2 mouse) {
            Vector2 position = new((mouse.x - rect.x) / rect.width, 1f - (mouse.y - rect.y) / rect.height);
            _activeStroke.Add(new SignatureInputPoint(position, (float)EditorApplication.timeSinceStartup));
            IncrementRevision();
        }
        private void IncrementRevision() { unchecked { Revision++; } }
        private static Vector2 ToCanvas(Rect rect, Vector2 normalized) =>
            new(rect.x + normalized.x * rect.width, rect.y + (1f - normalized.y) * rect.height);
    }
}
