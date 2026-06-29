using System;
using Types.Values;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Values {
    [CustomPropertyDrawer(typeof(Value))]
    public sealed class ValuePropertyDrawer : PropertyDrawer {
        private const float _PreviewSpacing = 2f;
        private const float _InputSpacing = 4f;
        private const float _DegreeWidth = 80f;

        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            var storedProperty = property.FindPropertyRelative("_stored");
            var degreeProperty = property.FindPropertyRelative("_base").FindPropertyRelative("Degree");

            var container = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Column,
                    marginTop = 1,
                    marginBottom = 1
                }
            };

            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };

            var label = new Label(property.displayName) {
                tooltip = property.tooltip,
                style = {
                    minWidth = EditorGUIUtility.labelWidth - 15,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };

            var inputs = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexGrow = 1
                }
            };

            var storedField = new DoubleField {
                tooltip = "Stored base value.",
                style = {
                    flexGrow = 1,
                    marginRight = _InputSpacing
                }
            };
            storedField.BindProperty(storedProperty);

            var degreeField = new IntegerField {
                tooltip = "Base degree.",
                style = {
                    width = _DegreeWidth
                }
            };
            degreeField.BindProperty(degreeProperty);

            var preview = new Label {
                style = {
                    marginLeft = EditorGUIUtility.labelWidth - 15,
                    marginTop = _PreviewSpacing,
                    unityFontStyleAndWeight = FontStyle.Italic
                }
            };

            void RefreshPreview(double stored, int degree) {
                preview.text = FormatPreview(stored, degree);
            }

            storedField.RegisterValueChangedCallback(evt => {
                var stored = evt.newValue;
                if (stored < 0d || double.IsNaN(stored) || double.IsInfinity(stored)) {
                    stored = 0d;
                    storedProperty.doubleValue = stored;
                    storedProperty.serializedObject.ApplyModifiedProperties();
                    storedField.SetValueWithoutNotify(stored);
                }

                RefreshPreview(stored, degreeField.value);
            });

            degreeField.RegisterValueChangedCallback(evt => {
                var degree = Mathf.Max(0, evt.newValue);
                if (degree != evt.newValue) {
                    degreeProperty.intValue = degree;
                    degreeProperty.serializedObject.ApplyModifiedProperties();
                    degreeField.SetValueWithoutNotify(degree);
                }

                RefreshPreview(storedField.value, degree);
            });

            inputs.Add(storedField);
            inputs.Add(degreeField);
            row.Add(label);
            row.Add(inputs);
            container.Add(row);
            container.Add(preview);

            RefreshPreview(storedProperty.doubleValue, degreeProperty.intValue);
            return container;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            var storedProperty = property.FindPropertyRelative("_stored");
            var degreeProperty = property.FindPropertyRelative("_base").FindPropertyRelative("Degree");

            EditorGUI.BeginProperty(position, label, property);

            var lineHeight = EditorGUIUtility.singleLineHeight;
            var inputLine = new Rect(position.x, position.y, position.width, lineHeight);
            var inputRect = EditorGUI.PrefixLabel(inputLine, label);
            var degreeWidth = Mathf.Min(_DegreeWidth, inputRect.width * 0.4f);
            var storedWidth = Mathf.Max(0f, inputRect.width - degreeWidth - _InputSpacing);
            var storedRect = new Rect(inputRect.x, inputRect.y, storedWidth, lineHeight);
            var degreeRect = new Rect(storedRect.xMax + _InputSpacing, inputRect.y, degreeWidth, lineHeight);

            storedProperty.doubleValue = Math.Max(0d, EditorGUI.DoubleField(storedRect, GUIContent.none, storedProperty.doubleValue));
            degreeProperty.intValue = Mathf.Max(0, EditorGUI.IntField(degreeRect, GUIContent.none, degreeProperty.intValue));

            var previewRect = new Rect(inputRect.x, inputLine.yMax + _PreviewSpacing, inputRect.width, lineHeight);
            EditorGUI.LabelField(previewRect, FormatPreview(storedProperty.doubleValue, degreeProperty.intValue), EditorStyles.miniLabel);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            return EditorGUIUtility.singleLineHeight * 2f + _PreviewSpacing;
        }

        private static string FormatPreview(double stored, int degree) {
            var safeStored = double.IsNaN(stored) || double.IsInfinity(stored) ? 0d : Math.Max(0d, stored);
            var safeDegree = Mathf.Max(0, degree);
            var value = new Value(safeStored, new Base { Degree = safeDegree });
            return $"In game: {value}";
        }
    }
}
