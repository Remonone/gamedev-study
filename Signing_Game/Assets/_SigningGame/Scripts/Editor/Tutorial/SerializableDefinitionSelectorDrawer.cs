using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Data.Tutorial;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SigningGame.Editor.Tutorial {
    /// <summary>
    /// Shared managed-reference selector drawer for tutorial definitions. Mirrors the selection UX of
    /// NumericValueDefinitionPropertyDrawer (type discovery across player assemblies, dropdown for null,
    /// header with clear button) without touching the numeric drawer itself.
    /// </summary>
    public abstract class SerializableDefinitionSelectorDrawer<TBase> : PropertyDrawer where TBase : class {
        private const string _SelectorPlaceholder = "Select definition...";
        private const string _NoCandidatesMessage =
            "No serializable implementations are available in player assemblies.";
        private const string _MultiTargetMessage = "Managed-reference type editing is disabled for multiple targets.";
        private const float _HeaderPadding = 3f;
        private const float _ClearButtonWidth = 20f;
        private const float _HelpBoxHeightInLines = 2f;

        private static readonly Candidate[] _Candidates = DiscoverCandidates();
        private static readonly string[] _SelectorOptions = BuildSelectorOptions();
        private static readonly GUIContent[] _SelectorGuiOptions = BuildSelectorGuiOptions();

        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            property = PrepareProperty(property);
            var serializedObject = property.serializedObject;
            var propertyPath = property.propertyPath;
            var fieldLabel = property.displayName;
            var fieldTooltip = property.tooltip;
            var root = new VisualElement();
            var structuralKey = GetStructuralState(property).Key;
            var rebuildQueued = false;

            void Rebuild(SerializedProperty liveProperty) {
                root.Clear();
                BuildVisualTree(root, liveProperty, fieldLabel, fieldTooltip, RequestRebuild);
            }

            void RequestRebuild() {
                if (!TryGetLiveProperty(serializedObject, propertyPath, out var liveProperty)) return;
                if (EnsureUniqueArrayReference(liveProperty)) {
                    serializedObject.ApplyModifiedProperties();
                    serializedObject.Update();
                    if (!TryGetLiveProperty(serializedObject, propertyPath, out liveProperty)) return;
                }
                if (GetStructuralState(liveProperty).Key == structuralKey || rebuildQueued) return;

                rebuildQueued = true;
                root.schedule.Execute(() => {
                    rebuildQueued = false;
                    if (!TryGetLiveProperty(serializedObject, propertyPath, out var deferredProperty)) return;
                    if (EnsureUniqueArrayReference(deferredProperty)) {
                        serializedObject.ApplyModifiedProperties();
                        serializedObject.Update();
                        if (!TryGetLiveProperty(serializedObject, propertyPath, out deferredProperty)) return;
                    }

                    var deferredKey = GetStructuralState(deferredProperty).Key;
                    if (deferredKey == structuralKey) return;

                    structuralKey = deferredKey;
                    Rebuild(deferredProperty);
                });
            }

            Rebuild(property);
            BindingExtensions.TrackPropertyValue(root, property, _ => RequestRebuild());
            return root;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            property = PrepareProperty(property);
            EditorGUI.BeginProperty(position, label, property);

            var state = GetStructuralState(property);
            switch (state.Kind) {
                case StructuralKind.Multi:
                    DrawMultiTarget(position, label);
                    break;
                case StructuralKind.Null:
                    DrawNull(position, property, label);
                    break;
                case StructuralKind.Missing:
                    DrawMissing(position, property, label, state.FullTypeName);
                    break;
                case StructuralKind.Assigned:
                    DrawAssigned(position, property, label);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            var state = GetStructuralState(property);
            if (state.Kind == StructuralKind.Assigned) {
                return EditorGUIUtility.singleLineHeight + VisitDirectVisibleChildren(property, null);
            }

            if (state.Kind == StructuralKind.Null && _Candidates.Length > 0) {
                return EditorGUIUtility.singleLineHeight;
            }

            return EditorGUIUtility.singleLineHeight
                   + EditorGUIUtility.standardVerticalSpacing
                   + GetHelpBoxHeight();
        }

        private static Candidate[] DiscoverCandidates() {
            var playerAssemblyNames = new HashSet<string>(
                CompilationPipeline.GetAssemblies(AssembliesType.Player).Select(assembly => assembly.name),
                StringComparer.Ordinal
            );

            var candidates = TypeCache.GetTypesDerivedFrom<TBase>()
                .Where(type => IsCandidate(type, playerAssemblyNames))
                .Select(type => new Candidate(type, ObjectNames.NicifyVariableName(type.Name)))
                .OrderBy(candidate => candidate.DisplayName, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Type.FullName ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Type.AssemblyQualifiedName ?? string.Empty, StringComparer.Ordinal)
                .ToArray();

            foreach (var displayGroup in candidates.GroupBy(candidate => candidate.DisplayName, StringComparer.Ordinal)) {
                var displayCollisions = displayGroup.ToArray();
                if (displayCollisions.Length < 2) continue;

                foreach (var candidate in displayCollisions) {
                    var namespaceName = string.IsNullOrEmpty(candidate.Type.Namespace)
                        ? "<global>"
                        : candidate.Type.Namespace;
                    candidate.OptionName = $"{candidate.DisplayName} ({namespaceName})";
                }

                foreach (var namespaceGroup in displayCollisions.GroupBy(candidate => candidate.OptionName,
                             StringComparer.Ordinal)) {
                    var namespaceCollisions = namespaceGroup.ToArray();
                    if (namespaceCollisions.Length < 2) continue;

                    foreach (var candidate in namespaceCollisions) {
                        candidate.OptionName = $"{candidate.OptionName} [{candidate.Type.Assembly.GetName().Name}]";
                    }
                }
            }

            foreach (var optionGroup in candidates.GroupBy(candidate => candidate.OptionName, StringComparer.Ordinal)) {
                var optionCollisions = optionGroup.ToArray();
                if (optionCollisions.Length < 2) continue;

                foreach (var candidate in optionCollisions) {
                    var fullTypeName = candidate.Type.FullName ?? candidate.Type.Name;
                    candidate.OptionName = $"{candidate.OptionName} [{fullTypeName}, {candidate.Type.Assembly.FullName}]";
                }
            }

            var usedOptionNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var candidate in candidates) {
                var optionName = candidate.OptionName;
                var duplicateIndex = 2;
                while (!usedOptionNames.Add(optionName)) {
                    optionName = $"{candidate.OptionName} #{duplicateIndex++}";
                }

                candidate.OptionName = optionName;
            }

            return candidates;
        }

        private static bool IsCandidate(Type type, ISet<string> playerAssemblyNames) {
            if (type == null
                || !typeof(TBase).IsAssignableFrom(type)
                || !type.IsClass
                || !type.IsSerializable
                || type.IsAbstract
                || type.IsGenericType
                || type.ContainsGenericParameters
                || !playerAssemblyNames.Contains(type.Assembly.GetName().Name)) {
                return false;
            }

            return type.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null
            ) != null;
        }

        private static string[] BuildSelectorOptions() {
            var options = new string[_Candidates.Length + 1];
            options[0] = _SelectorPlaceholder;
            for (var i = 0; i < _Candidates.Length; i++) {
                options[i + 1] = _Candidates[i].OptionName;
            }

            return options;
        }

        private static GUIContent[] BuildSelectorGuiOptions() {
            return _SelectorOptions.Select(option => new GUIContent(option)).ToArray();
        }

        private static StructuralState GetStructuralState(SerializedProperty property) {
            if (property.serializedObject.targetObjects.Length != 1) {
                return new StructuralState(StructuralKind.Multi, string.Empty);
            }

            var value = property.managedReferenceValue;
            var fullTypeName = property.managedReferenceFullTypename;
            if (value != null) {
                if (string.IsNullOrEmpty(fullTypeName)) {
                    fullTypeName = value.GetType().AssemblyQualifiedName ?? value.GetType().FullName ?? value.GetType().Name;
                }

                return new StructuralState(StructuralKind.Assigned, fullTypeName);
            }

            return string.IsNullOrEmpty(fullTypeName)
                ? new StructuralState(StructuralKind.Null, string.Empty)
                : new StructuralState(StructuralKind.Missing, fullTypeName);
        }

        private static void BuildVisualTree(
            VisualElement root,
            SerializedProperty property,
            string fieldLabel,
            string fieldTooltip,
            Action requestRebuild
        ) {
            var state = GetStructuralState(property);
            switch (state.Kind) {
                case StructuralKind.Multi:
                    BuildMultiTargetVisualTree(root, fieldLabel, fieldTooltip);
                    break;
                case StructuralKind.Null:
                    BuildNullVisualTree(root, property, fieldLabel, fieldTooltip, requestRebuild);
                    break;
                case StructuralKind.Missing:
                    BuildMissingVisualTree(root, property, fieldLabel, fieldTooltip, state.FullTypeName,
                        requestRebuild);
                    break;
                case StructuralKind.Assigned:
                    BuildAssignedVisualTree(root, property, fieldLabel, fieldTooltip, requestRebuild);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static void BuildNullVisualTree(
            VisualElement root,
            SerializedProperty property,
            string fieldLabel,
            string fieldTooltip,
            Action requestRebuild
        ) {
            var selector = new DropdownField(fieldLabel, new List<string>(_SelectorOptions), 0) {
                tooltip = fieldTooltip
            };
            if (_Candidates.Length == 0) {
                selector.SetEnabled(false);
                root.Add(selector);
                root.Add(new HelpBox(_NoCandidatesMessage, HelpBoxMessageType.Info));
                return;
            }

            var serializedObject = property.serializedObject;
            var propertyPath = property.propertyPath;
            selector.RegisterValueChangedCallback(evt => {
                var candidate = FindCandidate(evt.newValue);
                if (candidate == null) return;

                if (TryAssignCandidate(serializedObject, propertyPath, candidate)) {
                    requestRebuild();
                } else {
                    selector.SetValueWithoutNotify(_SelectorPlaceholder);
                }
            });
            root.Add(selector);
        }

        private static void BuildAssignedVisualTree(
            VisualElement root,
            SerializedProperty property,
            string fieldLabel,
            string fieldTooltip,
            Action requestRebuild
        ) {
            var typeDisplayName = GetTypeDisplayName(property.managedReferenceValue.GetType());
            var header = CreateHeader($"{fieldLabel} — {typeDisplayName}", fieldTooltip, false);
            var serializedObject = property.serializedObject;
            var propertyPath = property.propertyPath;
            header.Add(CreateClearButton("Clear definition.", () => {
                if (TryClearAssigned(serializedObject, propertyPath)) requestRebuild();
            }));
            root.Add(header);

            VisitDirectVisibleChildren(property, (child, _) => {
                var childField = new PropertyField(child);
                childField.BindProperty(child);
                root.Add(childField);
            });
        }

        private static void BuildMissingVisualTree(
            VisualElement root,
            SerializedProperty property,
            string fieldLabel,
            string fieldTooltip,
            string fullTypeName,
            Action requestRebuild
        ) {
            var header = CreateHeader($"{fieldLabel} — Missing: {fullTypeName}", fullTypeName, true);
            var serializedObject = property.serializedObject;
            var propertyPath = property.propertyPath;
            header.tooltip = string.IsNullOrEmpty(fieldTooltip)
                ? fullTypeName
                : $"{fieldTooltip}\n{fullTypeName}";
            header.Add(CreateClearButton("Clear this missing managed reference.", () => {
                if (TryClearMissing(serializedObject, propertyPath)) requestRebuild();
            }));
            root.Add(header);
            root.Add(new HelpBox($"The managed-reference type is missing: {fullTypeName}", HelpBoxMessageType.Warning));
        }

        private static void BuildMultiTargetVisualTree(VisualElement root, string fieldLabel, string fieldTooltip) {
            var header = CreateHeader($"{fieldLabel} — Multiple targets", fieldTooltip, false);
            header.SetEnabled(false);
            root.Add(header);
            root.Add(new HelpBox(_MultiTargetMessage, HelpBoxMessageType.Info));
        }

        private static VisualElement CreateHeader(string text, string tooltip, bool warning) {
            var header = new VisualElement {
                tooltip = tooltip,
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    backgroundColor = warning ? new Color(0.4f, 0.25f, 0f, 0.25f) : new Color(0f, 0f, 0f, 0.14f),
                    paddingLeft = _HeaderPadding,
                    paddingRight = _HeaderPadding,
                    paddingTop = 1f,
                    paddingBottom = 1f
                }
            };
            header.Add(new Label(text) {
                style = {
                    flexGrow = 1f,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            });
            return header;
        }

        private static Button CreateClearButton(string tooltip, Action onClick) {
            return new Button(onClick) {
                text = "x",
                tooltip = tooltip,
                style = {
                    width = _ClearButtonWidth,
                    minWidth = _ClearButtonWidth,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    marginLeft = 2f
                }
            };
        }

        private static void DrawNull(Rect position, SerializedProperty property, GUIContent label) {
            var selectorRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            if (_Candidates.Length == 0) {
                using (new EditorGUI.DisabledScope(true)) {
                    EditorGUI.Popup(selectorRect, label, 0, _SelectorGuiOptions);
                }

                var helpRect = GetHelpBoxRect(position);
                EditorGUI.HelpBox(helpRect, _NoCandidatesMessage, MessageType.Info);
                return;
            }

            var selectedIndex = EditorGUI.Popup(selectorRect, label, 0, _SelectorGuiOptions);
            if (selectedIndex <= 0 || selectedIndex > _Candidates.Length) return;
            TryAssignCandidate(property.serializedObject, property.propertyPath, _Candidates[selectedIndex - 1]);
        }

        private static void DrawAssigned(Rect position, SerializedProperty property, GUIContent label) {
            var headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var typeDisplayName = GetTypeDisplayName(property.managedReferenceValue.GetType());
            DrawHeader(headerRect, new GUIContent($"{label.text} — {typeDisplayName}", label.tooltip), false, false,
                out var clearRect);

            if (GUI.Button(clearRect, new GUIContent("x", "Clear definition."), EditorStyles.miniButton)) {
                TryClearAssigned(property.serializedObject, property.propertyPath);
                return;
            }

            var childY = headerRect.yMax;
            VisitDirectVisibleChildren(property, (child, childHeight) => {
                childY += EditorGUIUtility.standardVerticalSpacing;
                var childRect = new Rect(position.x, childY, position.width, childHeight);
                EditorGUI.PropertyField(childRect, child, true);
                childY += childHeight;
            });
        }

        private static void DrawMissing(Rect position, SerializedProperty property, GUIContent label,
            string fullTypeName) {
            var headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var tooltip = string.IsNullOrEmpty(label.tooltip) ? fullTypeName : $"{label.tooltip}\n{fullTypeName}";
            DrawHeader(headerRect, new GUIContent($"{label.text} — Missing: {fullTypeName}", tooltip), true, false,
                out var clearRect);

            if (GUI.Button(clearRect, new GUIContent("x", "Clear this missing managed reference."),
                    EditorStyles.miniButton)) {
                TryClearMissing(property.serializedObject, property.propertyPath);
            }

            EditorGUI.HelpBox(GetHelpBoxRect(position), $"The managed-reference type is missing: {fullTypeName}",
                MessageType.Warning);
        }

        private static void DrawMultiTarget(Rect position, GUIContent label) {
            var headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            DrawHeader(headerRect, new GUIContent($"{label.text} — Multiple targets", label.tooltip), false, true,
                out _);
            EditorGUI.HelpBox(GetHelpBoxRect(position), _MultiTargetMessage, MessageType.Info);
        }

        private static void DrawHeader(
            Rect rect,
            GUIContent content,
            bool warning,
            bool disabled,
            out Rect clearRect
        ) {
            EditorGUI.DrawRect(rect,
                warning ? new Color(0.4f, 0.25f, 0f, 0.25f) : new Color(0f, 0f, 0f, 0.14f));
            clearRect = new Rect(rect.xMax - _ClearButtonWidth, rect.y, _ClearButtonWidth, rect.height);
            var labelRect = new Rect(rect.x + _HeaderPadding, rect.y, rect.width - _ClearButtonWidth - _HeaderPadding * 2f,
                rect.height);
            using (new EditorGUI.DisabledScope(disabled)) {
                EditorGUI.LabelField(labelRect, content, EditorStyles.boldLabel);
            }
        }

        private static Rect GetHelpBoxRect(Rect position) {
            return new Rect(
                position.x,
                position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing,
                position.width,
                GetHelpBoxHeight()
            );
        }

        private static float GetHelpBoxHeight() {
            return EditorGUIUtility.singleLineHeight * _HelpBoxHeightInLines;
        }

        private static float VisitDirectVisibleChildren(
            SerializedProperty property,
            Action<SerializedProperty, float> visitor
        ) {
            var additionalHeight = 0f;
            var iterator = property.Copy();
            var endProperty = iterator.GetEndProperty();
            var parentDepth = property.depth;
            var enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty)) {
                enterChildren = false;
                if (iterator.depth != parentDepth + 1) continue;

                var child = iterator.Copy();
                var childHeight = EditorGUI.GetPropertyHeight(child, true);
                additionalHeight += EditorGUIUtility.standardVerticalSpacing + childHeight;
                visitor?.Invoke(child, childHeight);
            }

            return additionalHeight;
        }

        private static Candidate FindCandidate(string optionName) {
            for (var i = 0; i < _Candidates.Length; i++) {
                if (_Candidates[i].OptionName == optionName) return _Candidates[i];
            }

            return null;
        }

        private static SerializedProperty PrepareProperty(SerializedProperty property) {
            var serializedObject = property.serializedObject;
            var propertyPath = property.propertyPath;
            if (!EnsureUniqueArrayReference(property)) return property;

            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
            return serializedObject.FindProperty(propertyPath) ?? property;
        }

        private static bool EnsureUniqueArrayReference(SerializedProperty property) {
            if (property == null
                || property.serializedObject.isEditingMultipleObjects
                || property.propertyType != SerializedPropertyType.ManagedReference
                || property.managedReferenceValue is not TBase value
                || !TryGetArrayElementInfo(property.propertyPath, out var arrayPath, out var index, out var suffix)
                || index <= 0) {
                return false;
            }

            long referenceId = property.managedReferenceId;
            for (var i = 0; i < index; i++) {
                SerializedProperty sibling =
                    property.serializedObject.FindProperty($"{arrayPath}.Array.data[{i}]{suffix}");
                if (sibling?.propertyType != SerializedPropertyType.ManagedReference) continue;
                bool sameAssignedId = referenceId > 0L && sibling.managedReferenceId == referenceId;
                bool sameObject = ReferenceEquals(sibling.managedReferenceValue, value);
                if (!sameAssignedId && !sameObject) continue;

                try {
                    Undo.RecordObjects(property.serializedObject.targetObjects, "Detach Definition Reference");
                    property.managedReferenceValue = CloneObject(value,
                        new Dictionary<object, object>(ReferenceComparer.Instance));
                    return true;
                } catch (Exception exception) {
                    Debug.LogException(exception);
                    return false;
                }
            }

            return false;
        }

        private static bool TryAssignCandidate(SerializedObject serializedObject, string propertyPath,
            Candidate candidate) {
            object instance;
            try {
                instance = Activator.CreateInstance(candidate.Type, true);
            } catch (Exception exception) {
                Debug.LogException(exception);
                return false;
            }

            if (!TryGetLiveProperty(serializedObject, propertyPath, out var liveProperty)
                || GetStructuralState(liveProperty).Kind != StructuralKind.Null) {
                return false;
            }

            try {
                liveProperty.managedReferenceValue = instance;
                liveProperty.serializedObject.ApplyModifiedProperties();
                return true;
            } catch (Exception exception) {
                TryRestoreNullAfterFailedAssignment(serializedObject, propertyPath);
                Debug.LogException(exception);
                return false;
            }
        }

        private static void TryRestoreNullAfterFailedAssignment(SerializedObject serializedObject,
            string propertyPath) {
            try {
                if (!TryGetLiveProperty(serializedObject, propertyPath, out var liveProperty)) return;

                liveProperty.managedReferenceValue = null;
                liveProperty.serializedObject.ApplyModifiedProperties();
            } catch (Exception exception) {
                Debug.LogException(exception);
            }
        }

        private static bool TryClearAssigned(SerializedObject serializedObject, string propertyPath) {
            if (!TryGetLiveProperty(serializedObject, propertyPath, out var liveProperty)
                || GetStructuralState(liveProperty).Kind != StructuralKind.Assigned) {
                return false;
            }

            try {
                liveProperty.managedReferenceValue = null;
                liveProperty.serializedObject.ApplyModifiedProperties();
                return true;
            } catch (Exception exception) {
                Debug.LogException(exception);
                return false;
            }
        }

        private static bool TryClearMissing(SerializedObject serializedObject, string propertyPath) {
            if (!TryGetLiveProperty(serializedObject, propertyPath, out var liveProperty)
                || GetStructuralState(liveProperty).Kind != StructuralKind.Missing) {
                return false;
            }

            try {
                serializedObject.ApplyModifiedProperties();
                if (!TryGetLiveProperty(serializedObject, propertyPath, out liveProperty)
                    || GetStructuralState(liveProperty).Kind != StructuralKind.Missing) {
                    return false;
                }

                var targetObject = serializedObject.targetObject;
                var managedReferenceId = liveProperty.managedReferenceId;
                Undo.RegisterCompleteObjectUndo(targetObject, "Clear Missing Definition Reference");
                if (!SerializationUtility.ClearManagedReferenceWithMissingType(targetObject, managedReferenceId)) {
                    return false;
                }

                EditorUtility.SetDirty(targetObject);
                serializedObject.Update();
                return true;
            } catch (Exception exception) {
                Debug.LogException(exception);
                return false;
            }
        }

        private static bool TryGetLiveProperty(SerializedObject serializedObject, string propertyPath,
            out SerializedProperty property) {
            property = null;
            if (serializedObject == null || string.IsNullOrEmpty(propertyPath)) return false;

            try {
                var targets = serializedObject.targetObjects;
                if (targets == null || targets.Length != 1 || targets[0] == null) return false;

                property = serializedObject.FindProperty(propertyPath);
                return property != null && property.propertyType == SerializedPropertyType.ManagedReference;
            } catch {
                property = null;
                return false;
            }
        }

        private static bool TryGetArrayElementInfo(string propertyPath, out string arrayPath, out int index,
            out string suffix) {
            const string arrayToken = ".Array.data[";
            arrayPath = null;
            index = -1;
            suffix = null;

            var tokenIndex = propertyPath.LastIndexOf(arrayToken, StringComparison.Ordinal);
            if (tokenIndex < 0) return false;

            var indexStart = tokenIndex + arrayToken.Length;
            var indexEnd = propertyPath.IndexOf(']', indexStart);
            if (indexEnd < 0 || !int.TryParse(propertyPath.Substring(indexStart, indexEnd - indexStart), out index)) {
                return false;
            }

            arrayPath = propertyPath.Substring(0, tokenIndex);
            suffix = propertyPath.Substring(indexEnd + 1);
            return true;
        }

        private static object CloneObject(object value, Dictionary<object, object> visited) {
            if (value == null) return null;

            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type.IsValueType) {
                return value;
            }

            if (value is UnityEngine.Object) return value;
            if (visited.TryGetValue(value, out var knownClone)) return knownClone;

            if (value is AnimationCurve curve) {
                var curveClone = new AnimationCurve(curve.keys) {
                    preWrapMode = curve.preWrapMode,
                    postWrapMode = curve.postWrapMode
                };
                visited[value] = curveClone;
                return curveClone;
            }

            if (type.IsArray) {
                var sourceArray = (Array)value;
                Type elementType = type.GetElementType();
                var arrayClone = Array.CreateInstance(elementType, sourceArray.Length);
                visited[value] = arrayClone;

                for (var i = 0; i < sourceArray.Length; i++) {
                    arrayClone.SetValue(CloneObject(sourceArray.GetValue(i), visited), i);
                }

                return arrayClone;
            }

            object clone = Activator.CreateInstance(type);
            visited[value] = clone;
            foreach (FieldInfo field in GetSerializedFields(type)) {
                field.SetValue(clone, CloneObject(field.GetValue(value), visited));
            }

            return clone;
        }

        private static IEnumerable<FieldInfo> GetSerializedFields(Type type) {
            for (var current = type; current != null && current != typeof(object); current = current.BaseType) {
                FieldInfo[] fields = current.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                foreach (FieldInfo field in fields) {
                    if (field.IsStatic || field.IsInitOnly || field.IsNotSerialized) continue;
                    if (field.IsPublic || field.GetCustomAttribute<SerializeField>() != null ||
                        field.GetCustomAttribute<SerializeReference>() != null) {
                        yield return field;
                    }
                }
            }
        }

        private static string GetTypeDisplayName(Type type) {
            return ObjectNames.NicifyVariableName(type.Name);
        }

        private enum StructuralKind {
            Multi,
            Null,
            Missing,
            Assigned
        }

        private readonly struct StructuralState {
            public readonly StructuralKind Kind;
            public readonly string FullTypeName;
            public string Key => $"{Kind}:{FullTypeName}";

            public StructuralState(StructuralKind kind, string fullTypeName) {
                Kind = kind;
                FullTypeName = fullTypeName;
            }
        }

        private sealed class Candidate {
            public readonly Type Type;
            public readonly string DisplayName;
            public string OptionName;

            public Candidate(Type type, string displayName) {
                Type = type;
                DisplayName = displayName;
                OptionName = displayName;
            }
        }

        private sealed class ReferenceComparer : IEqualityComparer<object> {
            public static readonly ReferenceComparer Instance = new();

            public new bool Equals(object x, object y) {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj) {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }

    [CustomPropertyDrawer(typeof(TutorialTriggerDefinition), true)]
    internal sealed class TutorialTriggerDefinitionDrawer : SerializableDefinitionSelectorDrawer<TutorialTriggerDefinition> { }

    [CustomPropertyDrawer(typeof(TutorialSlideCondition), true)]
    internal sealed class TutorialSlideConditionDrawer : SerializableDefinitionSelectorDrawer<TutorialSlideCondition> { }
}
