using System;
using System.Collections.Generic;
using System.Linq;
using Constants;
using Data.Bills;
using Data.Formulas;
using Data.Modifiers;
using Data.Upgrades;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace SigningGame.Editor.UpgradeTree {
    internal enum UpgradeTreeFaultPoint {
        None,
        AfterPersistBeforeVerification,
        AfterMoveAndPersistBeforeVerification,
        AfterStagingPersistedBeforeFolderDelete
    }

    internal readonly struct UpgradeTreeOperationResult<T> {
        internal readonly T Value;
        internal readonly string Error;
        internal bool Success => string.IsNullOrEmpty(Error);

        internal UpgradeTreeOperationResult(T value, string error = null) {
            Value = value;
            Error = error;
        }

        internal static UpgradeTreeOperationResult<T> Fail(string error) => new(default, error);
    }

    internal sealed class UpgradeTreeEditorOperations {
        internal static UpgradeTreeFaultPoint TestFaultPoint { get; set; }

        private readonly UpgradeTreeEditorSettings _settings;
        private readonly AddressableAssetSettings _addressables;

        internal UpgradeTreeEditorOperations(UpgradeTreeEditorSettings settings, AddressableAssetSettings addressables) {
            _settings = settings;
            _addressables = addressables;
        }

        internal IReadOnlyList<UpgradeNodeDefinition> DiscoverNodes() {
            if (_settings == null || !AssetDatabase.IsValidFolder(_settings.UpgradeRootPath)) {
                return Array.Empty<UpgradeNodeDefinition>();
            }

            return AssetDatabase.FindAssets("t:UpgradeNodeDefinition", new[] { _settings.UpgradeRootPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<UpgradeNodeDefinition>)
                .Where(node => node != null)
                .OrderBy(node => node.Id ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal)
                .ToArray();
        }

        internal UpgradeTreeOperationResult<UpgradeNodeDefinition> CreateNode(
            string id,
            Vector2 position,
            UpgradeNodeDefinition source = null
        ) {
            if (!TryPreflightNewNode(id, out string folderPath, out AddressableAssetGroup group, out string error)) {
                return UpgradeTreeOperationResult<UpgradeNodeDefinition>.Fail(error);
            }
            if (source != null && !TryRequireCleanEditable(source, out error)) {
                return UpgradeTreeOperationResult<UpgradeNodeDefinition>.Fail(error);
            }

            string nodePath = $"{folderPath}/Upgrade Node.asset";
            UpgradeNodeDefinition node = null;
            string guid = null;
            UpgradeNodeLink[] sourceSnapshot = source?.Children ?? Array.Empty<UpgradeNodeLink>();
            try {
                EnsureFolder(_settings.UpgradeRootPath);
                string parent = GetParentPath(folderPath);
                string folderName = GetName(folderPath);
                if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, folderName))) {
                    throw new InvalidOperationException($"Failed to create folder '{folderPath}'.");
                }

                node = ScriptableObject.CreateInstance<UpgradeNodeDefinition>();
                node.Id = id;
                node.Name = id;
                node.CostFormula = new ConstantValue();
                node.Modifiers = Array.Empty<ModifierDefinition>();
                node.FeatureUnlockIds = Array.Empty<string>();
                node.StatisticRequirements = Array.Empty<GameStatisticRequirement>();
                node.Children = Array.Empty<UpgradeNodeLink>();
                node.TreePosition = position;
                AssetDatabase.CreateAsset(node, nodePath);
                guid = AssetDatabase.AssetPathToGUID(nodePath);
                AddressableAssetEntry entry = _addressables.CreateOrMoveEntry(guid, group, false, false);
                entry.SetAddress(nodePath, false);
                foreach (string label in EnumerateLabels()) entry.SetLabel(label, true, false, false);
                if (source != null) {
                    source.Children = sourceSnapshot.Concat(new[] {
                        new UpgradeNodeLink { Child = node, DrawEdge = true }
                    }).ToArray();
                    EditorUtility.SetDirty(source);
                }
                SaveTouched(node, source, group, _addressables);
                if (TestFaultPoint == UpgradeTreeFaultPoint.AfterPersistBeforeVerification) {
                    throw new InvalidOperationException("Injected create fault.");
                }

                AssetDatabase.ImportAsset(nodePath, ImportAssetOptions.ForceUpdate);
                node = AssetDatabase.LoadAssetAtPath<UpgradeNodeDefinition>(nodePath);
                AddressableAssetEntry verified = _addressables.FindAssetEntry(guid);
                if (node == null || verified == null || !verified.labels.Contains(AddressableConstants.UPGRADE_LABEL)) {
                    throw new InvalidOperationException("Created node failed persistence verification.");
                }

                return new UpgradeTreeOperationResult<UpgradeNodeDefinition>(node);
            } catch (Exception exception) {
                if (source != null) {
                    source.Children = sourceSnapshot;
                    EditorUtility.SetDirty(source);
                    AssetDatabase.SaveAssetIfDirty(source);
                }
                if (!string.IsNullOrEmpty(guid)) _addressables?.RemoveAssetEntry(guid, false);
                if (AssetDatabase.IsValidFolder(folderPath)) AssetDatabase.DeleteAsset(folderPath);
                if (group != null) SaveTouched(group, _addressables);
                return UpgradeTreeOperationResult<UpgradeNodeDefinition>.Fail(exception.Message);
            }
        }

        internal UpgradeTreeOperationResult<ModifierDefinition> CreateModifier(
            UpgradeNodeDefinition node,
            string fileStem
        ) {
            if (node == null) return UpgradeTreeOperationResult<ModifierDefinition>.Fail("Select one node first.");
            if (!UpgradeTreeEditorValidation.TryValidateSegment(fileStem, "Modifier filename", out string error)) {
                return UpgradeTreeOperationResult<ModifierDefinition>.Fail(error);
            }

            if (!TryRequireCleanEditable(node, out error)) return UpgradeTreeOperationResult<ModifierDefinition>.Fail(error);
            string nodePath = AssetDatabase.GetAssetPath(node);
            string folder = GetParentPath(nodePath);
            string modifierPath = $"{folder}/{fileStem}.asset";
            if (PathExistsCaseInsensitive(modifierPath)) {
                return UpgradeTreeOperationResult<ModifierDefinition>.Fail($"Asset '{modifierPath}' already exists.");
            }

            ModifierDefinition modifier = null;
            try {
                modifier = ScriptableObject.CreateInstance<ModifierDefinition>();
                modifier.NumericModifiers = new List<NumericModifierDefinition>();
                AssetDatabase.CreateAsset(modifier, modifierPath);
                var serialized = new SerializedObject(node);
                SerializedProperty modifiers = serialized.FindProperty(nameof(UpgradeNodeDefinition.Modifiers));
                int index = modifiers.arraySize;
                modifiers.InsertArrayElementAtIndex(index);
                modifiers.GetArrayElementAtIndex(index).objectReferenceValue = modifier;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                SaveTouched(node, modifier);
                AssetDatabase.ImportAsset(modifierPath, ImportAssetOptions.ForceUpdate);
                return new UpgradeTreeOperationResult<ModifierDefinition>(
                    AssetDatabase.LoadAssetAtPath<ModifierDefinition>(modifierPath));
            } catch (Exception exception) {
                var serialized = new SerializedObject(node);
                SerializedProperty modifiers = serialized.FindProperty(nameof(UpgradeNodeDefinition.Modifiers));
                for (var index = modifiers.arraySize - 1; index >= 0; index--) {
                    if (modifiers.GetArrayElementAtIndex(index).objectReferenceValue != modifier) continue;
                    modifiers.DeleteArrayElementAtIndex(index);
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(node);
                if (AssetDatabase.LoadAssetAtPath<ModifierDefinition>(modifierPath) != null) AssetDatabase.DeleteAsset(modifierPath);
                return UpgradeTreeOperationResult<ModifierDefinition>.Fail(exception.Message);
            }
        }

        internal UpgradeTreeOperationResult<bool> AddEdge(
            UpgradeNodeDefinition source,
            UpgradeNodeDefinition child,
            bool drawEdge = true
        ) {
            if (source == null || child == null) return UpgradeTreeOperationResult<bool>.Fail("Both nodes are required.");
            if (source == child) return UpgradeTreeOperationResult<bool>.Fail("A node cannot link to itself.");
            if (GetChildren(source).Any(link => link.Child == child)) {
                return UpgradeTreeOperationResult<bool>.Fail("This direct edge already exists.");
            }

            if (WouldCreateCycle(source, child)) {
                return UpgradeTreeOperationResult<bool>.Fail("This edge would create a directed cycle.");
            }

            if (!TryRequireCleanEditable(source, out string error)) {
                return UpgradeTreeOperationResult<bool>.Fail(error);
            }

            Undo.RecordObject(source, "Add upgrade edge");
            var serialized = new SerializedObject(source);
            SerializedProperty children = serialized.FindProperty(nameof(UpgradeNodeDefinition.Children));
            int index = children.arraySize;
            children.InsertArrayElementAtIndex(index);
            SerializedProperty link = children.GetArrayElementAtIndex(index);
            link.FindPropertyRelative(nameof(UpgradeNodeLink.Child)).objectReferenceValue = child;
            link.FindPropertyRelative(nameof(UpgradeNodeLink.DrawEdge)).boolValue = drawEdge;
            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(source);
            return new UpgradeTreeOperationResult<bool>(true);
        }

        internal UpgradeTreeOperationResult<bool> RemoveEdge(
            UpgradeNodeDefinition source,
            UpgradeNodeDefinition child
        ) {
            if (source == null || child == null) return UpgradeTreeOperationResult<bool>.Fail("Both nodes are required.");
            if (!TryRequireCleanEditable(source, out string error)) {
                return UpgradeTreeOperationResult<bool>.Fail(error);
            }

            var serialized = new SerializedObject(source);
            SerializedProperty children = serialized.FindProperty(nameof(UpgradeNodeDefinition.Children));
            for (var index = 0; index < children.arraySize; index++) {
                if (children.GetArrayElementAtIndex(index)
                        .FindPropertyRelative(nameof(UpgradeNodeLink.Child)).objectReferenceValue != child) continue;
                Undo.RecordObject(source, "Remove upgrade edge");
                children.DeleteArrayElementAtIndex(index);
                serialized.ApplyModifiedProperties();
                AssetDatabase.SaveAssetIfDirty(source);
                return new UpgradeTreeOperationResult<bool>(true);
            }

            return UpgradeTreeOperationResult<bool>.Fail("The direct edge does not exist.");
        }

        internal UpgradeTreeOperationResult<bool> SetEdgeVisibility(
            UpgradeNodeDefinition source,
            UpgradeNodeDefinition child,
            bool drawEdge
        ) {
            if (!TryRequireCleanEditable(source, out string error)) {
                return UpgradeTreeOperationResult<bool>.Fail(error);
            }

            var serialized = new SerializedObject(source);
            SerializedProperty children = serialized.FindProperty(nameof(UpgradeNodeDefinition.Children));
            for (var index = 0; index < children.arraySize; index++) {
                SerializedProperty link = children.GetArrayElementAtIndex(index);
                if (link.FindPropertyRelative(nameof(UpgradeNodeLink.Child)).objectReferenceValue != child) continue;
                Undo.RecordObject(source, "Change upgrade edge visibility");
                link.FindPropertyRelative(nameof(UpgradeNodeLink.DrawEdge)).boolValue = drawEdge;
                serialized.ApplyModifiedProperties();
                AssetDatabase.SaveAssetIfDirty(source);
                return new UpgradeTreeOperationResult<bool>(true);
            }

            return UpgradeTreeOperationResult<bool>.Fail("The direct edge does not exist.");
        }

        internal UpgradeTreeOperationResult<bool> SetModifierReference(
            UpgradeNodeDefinition node,
            int index,
            ModifierDefinition value
        ) {
            if (node == null) return UpgradeTreeOperationResult<bool>.Fail("Select one node.");
            if (!TryRequireCleanEditable(node, out string error)) return UpgradeTreeOperationResult<bool>.Fail(error);
            var serialized = new SerializedObject(node);
            SerializedProperty modifiers = serialized.FindProperty(nameof(UpgradeNodeDefinition.Modifiers));
            if (index < 0 || index > modifiers.arraySize) {
                return UpgradeTreeOperationResult<bool>.Fail("Modifier index is out of range.");
            }
            Undo.RecordObject(node, "Change upgrade modifier");
            if (index == modifiers.arraySize) modifiers.InsertArrayElementAtIndex(index);
            modifiers.GetArrayElementAtIndex(index).objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(node);
            return new UpgradeTreeOperationResult<bool>(true);
        }

        internal UpgradeTreeOperationResult<bool> RemoveModifierReference(UpgradeNodeDefinition node, int index) {
            if (node == null) return UpgradeTreeOperationResult<bool>.Fail("Select one node.");
            if (!TryRequireCleanEditable(node, out string error)) return UpgradeTreeOperationResult<bool>.Fail(error);
            var serialized = new SerializedObject(node);
            SerializedProperty modifiers = serialized.FindProperty(nameof(UpgradeNodeDefinition.Modifiers));
            if (index < 0 || index >= modifiers.arraySize) {
                return UpgradeTreeOperationResult<bool>.Fail("Modifier index is out of range.");
            }
            Undo.RecordObject(node, "Remove upgrade modifier");
            modifiers.DeleteArrayElementAtIndex(index);
            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(node);
            return new UpgradeTreeOperationResult<bool>(true);
        }

        internal bool WouldCreateCycle(UpgradeNodeDefinition source, UpgradeNodeDefinition child) {
            if (source == null || child == null) return false;
            var visited = new HashSet<UpgradeNodeDefinition>();
            var stack = new Stack<UpgradeNodeDefinition>();
            stack.Push(child);
            while (stack.Count > 0) {
                UpgradeNodeDefinition current = stack.Pop();
                if (current == source) return true;
                if (current == null || !visited.Add(current)) continue;
                foreach (UpgradeNodeLink link in GetChildren(current)) {
                    if (link.Child != null) stack.Push(link.Child);
                }
            }

            return false;
        }

        internal UpgradeTreeOperationResult<IReadOnlyList<UpgradeNodeDefinition>> CopyNodes(
            IReadOnlyList<UpgradeNodeDefinition> sourceNodes,
            Vector2 offset
        ) {
            if (sourceNodes == null || sourceNodes.Count == 0) {
                return UpgradeTreeOperationResult<IReadOnlyList<UpgradeNodeDefinition>>.Fail("Clipboard is empty.");
            }

            var sources = sourceNodes.Where(node => node != null).Distinct().OrderBy(GetGuid, StringComparer.Ordinal).ToArray();
            if (sources.Length != sourceNodes.Count) {
                return UpgradeTreeOperationResult<IReadOnlyList<UpgradeNodeDefinition>>.Fail("A clipboard source is missing.");
            }

            foreach (UpgradeNodeDefinition source in sources) {
                if (!TryRequireCleanEditable(source, out string sourceError)) {
                    return UpgradeTreeOperationResult<IReadOnlyList<UpgradeNodeDefinition>>.Fail(sourceError);
                }
                foreach (ModifierDefinition modifier in source.Modifiers ?? Array.Empty<ModifierDefinition>()) {
                    if (modifier != null && !TryRequireCleanEditable(modifier, out sourceError)) {
                        return UpgradeTreeOperationResult<IReadOnlyList<UpgradeNodeDefinition>>.Fail(sourceError);
                    }
                }
            }

            if (ContainsCycle(sources)) {
                return UpgradeTreeOperationResult<IReadOnlyList<UpgradeNodeDefinition>>.Fail(
                    "The selected source subgraph already contains a cycle.");
            }

            if (!TryGetAddressablesContext(out AddressableAssetGroup group, out string error)) {
                return UpgradeTreeOperationResult<IReadOnlyList<UpgradeNodeDefinition>>.Fail(error);
            }

            var allocatedIds = new HashSet<string>(GetAllNodes().Select(node => node.Id ?? string.Empty), StringComparer.Ordinal);
            var allocatedPaths = new HashSet<string>(AssetDatabase.GetAllAssetPaths(), StringComparer.OrdinalIgnoreCase);
            var plans = new List<(UpgradeNodeDefinition source, string id, string folder, string path)>();
            foreach (UpgradeNodeDefinition source in sources) {
                int copyNumber = 1;
                string id;
                string folder;
                do {
                    id = $"{source.Id}_{copyNumber++}";
                    folder = $"{_settings.UpgradeRootPath}/{id}";
                } while (allocatedIds.Contains(id) || allocatedPaths.Contains(folder));
                if (!UpgradeTreeEditorValidation.TryValidateSegment(id, "Copied node ID", out error)) {
                    return UpgradeTreeOperationResult<IReadOnlyList<UpgradeNodeDefinition>>.Fail(error);
                }

                allocatedIds.Add(id);
                allocatedPaths.Add(folder);
                plans.Add((source, id, folder, $"{folder}/Upgrade Node.asset"));
            }

            var createdFolders = new List<string>();
            var createdEntries = new List<string>();
            try {
                var sourceToCopy = new Dictionary<UpgradeNodeDefinition, UpgradeNodeDefinition>();
                foreach (var plan in plans) {
                    if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(_settings.UpgradeRootPath, plan.id))) {
                        throw new InvalidOperationException($"Failed to create '{plan.folder}'.");
                    }
                    createdFolders.Add(plan.folder);
                    if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(plan.source), plan.path)) {
                        throw new InvalidOperationException($"Failed to copy '{plan.source.Id}'.");
                    }
                    UpgradeNodeDefinition copy = AssetDatabase.LoadAssetAtPath<UpgradeNodeDefinition>(plan.path);
                    sourceToCopy.Add(plan.source, copy);
                }

                var modifierCopies = new Dictionary<ModifierDefinition, ModifierDefinition>();
                foreach (var plan in plans) {
                    UpgradeNodeDefinition copy = sourceToCopy[plan.source];
                    ModifierDefinition[] sourceModifiers = plan.source.Modifiers ?? Array.Empty<ModifierDefinition>();
                    var copiedModifiers = new ModifierDefinition[sourceModifiers.Length];
                    for (var index = 0; index < sourceModifiers.Length; index++) {
                        ModifierDefinition sourceModifier = sourceModifiers[index];
                        if (sourceModifier == null) continue;
                        if (!modifierCopies.TryGetValue(sourceModifier, out ModifierDefinition modifierCopy)) {
                            string stem = System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(sourceModifier));
                            string targetPath = AllocateAssetPath(plan.folder, stem, allocatedPaths);
                            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(sourceModifier), targetPath)) {
                                throw new InvalidOperationException($"Failed to copy modifier '{sourceModifier.name}'.");
                            }
                            modifierCopy = AssetDatabase.LoadAssetAtPath<ModifierDefinition>(targetPath);
                            modifierCopies.Add(sourceModifier, modifierCopy);
                        }
                        copiedModifiers[index] = modifierCopy;
                    }

                    copy.Id = plan.id;
                    copy.Name = plan.source.Name;
                    copy.TreePosition = plan.source.TreePosition + offset;
                    copy.Modifiers = copiedModifiers;
                    UpgradeNodeLink[] sourceLinks = plan.source.Children ?? Array.Empty<UpgradeNodeLink>();
                    copy.Children = sourceLinks.Select(link => new UpgradeNodeLink {
                        Child = link.Child != null && sourceToCopy.TryGetValue(link.Child, out UpgradeNodeDefinition remapped)
                            ? remapped
                            : link.Child,
                        DrawEdge = link.DrawEdge
                    }).ToArray();
                    EditorUtility.SetDirty(copy);
                    AssetDatabase.SaveAssetIfDirty(copy);
                    string guid = AssetDatabase.AssetPathToGUID(plan.path);
                    AddressableAssetEntry entry = _addressables.CreateOrMoveEntry(guid, group, false, false);
                    entry.SetAddress(plan.path, false);
                    foreach (string label in EnumerateLabels()) entry.SetLabel(label, true, false, false);
                    createdEntries.Add(guid);
                }

                SaveTouched(group, _addressables);
                if (TestFaultPoint == UpgradeTreeFaultPoint.AfterPersistBeforeVerification) {
                    throw new InvalidOperationException("Injected copy fault.");
                }

                var verifiedCopies = new List<UpgradeNodeDefinition>(plans.Count);
                foreach (var plan in plans) {
                    AssetDatabase.ImportAsset(plan.path, ImportAssetOptions.ForceUpdate);
                    UpgradeNodeDefinition verifiedCopy = AssetDatabase.LoadAssetAtPath<UpgradeNodeDefinition>(plan.path);
                    string verifiedGuid = AssetDatabase.AssetPathToGUID(plan.path);
                    AddressableAssetEntry verifiedEntry = _addressables.FindAssetEntry(verifiedGuid);
                    if (verifiedCopy == null || !string.Equals(verifiedCopy.Id, plan.id, StringComparison.Ordinal) ||
                        verifiedEntry == null || verifiedEntry.parentGroup != group ||
                        !string.Equals(verifiedEntry.address, plan.path, StringComparison.Ordinal) ||
                        EnumerateLabels().Any(label => !verifiedEntry.labels.Contains(label))) {
                        throw new InvalidOperationException($"Copied node '{plan.id}' failed persistence verification.");
                    }
                    verifiedCopies.Add(verifiedCopy);
                }

                return new UpgradeTreeOperationResult<IReadOnlyList<UpgradeNodeDefinition>>(verifiedCopies);
            } catch (Exception exception) {
                foreach (string guid in createdEntries) _addressables.RemoveAssetEntry(guid, false);
                for (var index = createdFolders.Count - 1; index >= 0; index--) AssetDatabase.DeleteAsset(createdFolders[index]);
                SaveTouched(group, _addressables);
                return UpgradeTreeOperationResult<IReadOnlyList<UpgradeNodeDefinition>>.Fail(exception.Message);
            }
        }

        internal UpgradeTreeOperationResult<UpgradeNodeDefinition> RenameNode(
            UpgradeNodeDefinition node,
            string newId
        ) {
            if (node == null) return UpgradeTreeOperationResult<UpgradeNodeDefinition>.Fail("Select one node.");
            if (!UpgradeTreeEditorValidation.TryValidateSegment(newId, "Upgrade ID", out string error)) {
                return UpgradeTreeOperationResult<UpgradeNodeDefinition>.Fail(error);
            }
            if (string.Equals(node.Id, newId, StringComparison.Ordinal)) return new UpgradeTreeOperationResult<UpgradeNodeDefinition>(node);
            if (!TryPreflightOwnedFolder(node, true, out string oldFolder, out _, out error)) {
                return UpgradeTreeOperationResult<UpgradeNodeDefinition>.Fail(error);
            }
            if (GetAllNodes().Any(other => other != node && string.Equals(other.Id, newId, StringComparison.Ordinal))) {
                return UpgradeTreeOperationResult<UpgradeNodeDefinition>.Fail($"Upgrade ID '{newId}' already exists.");
            }
            if (FindBillReferences(node.Id).Count > 0) {
                return UpgradeTreeOperationResult<UpgradeNodeDefinition>.Fail(
                    $"Upgrade ID is referenced by bill requirements:\n{string.Join("\n", FindBillReferences(node.Id))}");
            }

            string newFolder = $"{_settings.UpgradeRootPath}/{newId}";
            if (PathExistsCaseInsensitive(newFolder)) {
                return UpgradeTreeOperationResult<UpgradeNodeDefinition>.Fail($"Folder '{newFolder}' already exists.");
            }

            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(node));
            AddressableAssetEntry entry = _addressables?.FindAssetEntry(guid);
            if (entry == null || entry.ReadOnly) {
                return UpgradeTreeOperationResult<UpgradeNodeDefinition>.Fail("Rename requires a writable Addressables entry.");
            }
            if (!TryRequireAddressablesCleanEditable(entry.parentGroup, out error)) {
                return UpgradeTreeOperationResult<UpgradeNodeDefinition>.Fail(error);
            }

            string oldId = node.Id;
            string oldAddress = entry.address;
            AddressableAssetGroup oldGroup = entry.parentGroup;
            string[] oldLabels = entry.labels.ToArray();
            string oldNodeName = System.IO.Path.GetFileName(AssetDatabase.GetAssetPath(node));
            try {
                string moveError = AssetDatabase.MoveAsset(oldFolder, newFolder);
                if (!string.IsNullOrEmpty(moveError)) throw new InvalidOperationException(moveError);
                string newNodePath = $"{newFolder}/{oldNodeName}";
                UpgradeNodeDefinition moved = AssetDatabase.LoadAssetAtPath<UpgradeNodeDefinition>(newNodePath);
                moved.Id = newId;
                EditorUtility.SetDirty(moved);
                entry.SetAddress(newNodePath, false);
                SaveTouched(moved, entry.parentGroup, _addressables);
                if (TestFaultPoint == UpgradeTreeFaultPoint.AfterMoveAndPersistBeforeVerification) {
                    throw new InvalidOperationException("Injected rename fault.");
                }
                AssetDatabase.ImportAsset(newNodePath, ImportAssetOptions.ForceUpdate);
                UpgradeNodeDefinition verified = AssetDatabase.LoadAssetAtPath<UpgradeNodeDefinition>(newNodePath);
                AddressableAssetEntry verifiedEntry = _addressables.FindAssetEntry(guid);
                if (verified == null || !string.Equals(verified.Id, newId, StringComparison.Ordinal) ||
                    verifiedEntry == null || verifiedEntry.parentGroup != oldGroup ||
                    !string.Equals(verifiedEntry.address, newNodePath, StringComparison.Ordinal) ||
                    oldLabels.Any(label => !verifiedEntry.labels.Contains(label))) {
                    throw new InvalidOperationException("Renamed node failed persistence verification.");
                }
                return new UpgradeTreeOperationResult<UpgradeNodeDefinition>(verified);
            } catch (Exception exception) {
                UpgradeNodeDefinition moved = AssetDatabase.LoadAssetAtPath<UpgradeNodeDefinition>($"{newFolder}/{oldNodeName}");
                if (moved != null) {
                    moved.Id = oldId;
                    EditorUtility.SetDirty(moved);
                    AssetDatabase.SaveAssetIfDirty(moved);
                }
                string reverseError = string.Empty;
                if (AssetDatabase.IsValidFolder(newFolder)) reverseError = AssetDatabase.MoveAsset(newFolder, oldFolder);
                bool folderRestored = string.IsNullOrEmpty(reverseError) && AssetDatabase.IsValidFolder(oldFolder);
                string survivingNodePath = folderRestored
                    ? $"{oldFolder}/{oldNodeName}"
                    : AssetDatabase.IsValidFolder(newFolder)
                        ? $"{newFolder}/{oldNodeName}"
                        : string.Empty;
                RestoreEntry(guid, oldGroup, folderRestored ? oldAddress : survivingNodePath, oldLabels);
                SaveTouched(oldGroup, _addressables);
                string rollbackMessage = folderRestored
                    ? string.Empty
                    : $" Rollback could not restore the original folder: {reverseError}";
                return UpgradeTreeOperationResult<UpgradeNodeDefinition>.Fail(exception.Message + rollbackMessage);
            }
        }

        internal UpgradeTreeOperationResult<bool> DeleteNode(UpgradeNodeDefinition node) {
            if (node == null) return UpgradeTreeOperationResult<bool>.Fail("Select exactly one node.");
            if (!TryPreflightOwnedFolder(node, false, out string folder, out string[] ownedPaths, out string error)) {
                return UpgradeTreeOperationResult<bool>.Fail(error);
            }
            List<string> billReferences = FindBillReferences(node.Id);
            if (billReferences.Count > 0) {
                return UpgradeTreeOperationResult<bool>.Fail(
                    $"Upgrade ID is referenced by bill requirements:\n{string.Join("\n", billReferences)}");
            }

            var inbound = GetAllNodes()
                .Where(parent => parent != node && GetChildren(parent).Any(link => link.Child == node))
                .ToArray();
            foreach (UpgradeNodeDefinition parent in inbound) {
                if (!TryRequireCleanEditable(parent, out error)) return UpgradeTreeOperationResult<bool>.Fail(error);
            }

            string nodeGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(node));
            AddressableAssetEntry entry = _addressables?.FindAssetEntry(nodeGuid);
            AddressableAssetGroup entryGroup = entry?.parentGroup;
            string oldAddress = entry?.address;
            string[] oldLabels = entry?.labels.ToArray();
            if (entryGroup != null && !TryRequireAddressablesCleanEditable(entryGroup, out error)) {
                return UpgradeTreeOperationResult<bool>.Fail(error);
            }

            var snapshots = inbound.ToDictionary(parent => parent, parent => parent.Children ?? Array.Empty<UpgradeNodeLink>());
            try {
                foreach (UpgradeNodeDefinition parent in inbound) {
                    parent.Children = GetChildren(parent).Where(link => link.Child != node).ToArray();
                    EditorUtility.SetDirty(parent);
                    AssetDatabase.SaveAssetIfDirty(parent);
                }
                if (entry != null) _addressables.RemoveAssetEntry(nodeGuid, false);
                SaveTouched(entryGroup, _addressables);
                if (TestFaultPoint == UpgradeTreeFaultPoint.AfterStagingPersistedBeforeFolderDelete) {
                    throw new InvalidOperationException("Injected delete fault.");
                }

                AssetDatabase.DeleteAsset(folder);
                bool folderGone = !AssetDatabase.IsValidFolder(folder);
                bool assetsGone = ownedPaths.All(path => AssetDatabase.LoadMainAssetAtPath(path) == null);
                if (folderGone && assetsGone) return new UpgradeTreeOperationResult<bool>(true);

                bool allRemain = ownedPaths.All(path => AssetDatabase.LoadMainAssetAtPath(path) != null);
                if (!allRemain) {
                    return UpgradeTreeOperationResult<bool>.Fail(
                        "Deletion was partial. Inbound links remain removed to avoid dangling references. Inspect the folder manually.");
                }
                throw new InvalidOperationException("Unity did not delete the upgrade folder.");
            } catch (Exception exception) {
                bool allRemain = ownedPaths.All(path => AssetDatabase.LoadMainAssetAtPath(path) != null);
                if (allRemain) {
                    foreach (var pair in snapshots) {
                        pair.Key.Children = pair.Value;
                        EditorUtility.SetDirty(pair.Key);
                        AssetDatabase.SaveAssetIfDirty(pair.Key);
                    }
                    if (entry != null || entryGroup != null) RestoreEntry(nodeGuid, entryGroup, oldAddress, oldLabels);
                    SaveTouched(entryGroup, _addressables);
                }
                return UpgradeTreeOperationResult<bool>.Fail(exception.Message);
            }
        }

        internal static bool CanDeleteSelection(int selectionCount) => selectionCount == 1;

        private bool TryPreflightNewNode(
            string id,
            out string folderPath,
            out AddressableAssetGroup group,
            out string error
        ) {
            folderPath = null;
            group = null;
            if (!UpgradeTreeEditorValidation.TryValidateSegment(id, "Upgrade ID", out error)) return false;
            if (!UpgradeTreeEditorValidation.TryValidateRootSuffix(_settings.UpgradeRootSuffix, out _, out error)) return false;
            if (GetAllNodes().Any(node => string.Equals(node.Id, id, StringComparison.Ordinal))) {
                error = $"Upgrade ID '{id}' already exists.";
                return false;
            }
            folderPath = $"{_settings.UpgradeRootPath}/{id}";
            if (PathExistsCaseInsensitive(folderPath)) {
                error = $"Folder '{folderPath}' already exists.";
                return false;
            }
            if (!TryGetAddressablesContext(out group, out error)) return false;
            if (!AssetDatabase.CanOpenForEdit(_settings.UpgradeRootPath, out string editMessage) &&
                AssetDatabase.IsValidFolder(_settings.UpgradeRootPath)) {
                error = $"Upgrade root is not editable: {editMessage}";
                return false;
            }
            string editableParent = _settings.UpgradeRootPath;
            while (!AssetDatabase.IsValidFolder(editableParent) && !string.IsNullOrEmpty(editableParent)) {
                editableParent = GetParentPath(editableParent);
            }
            if (string.IsNullOrEmpty(editableParent) ||
                !AssetDatabase.CanOpenForEdit(editableParent, out editMessage)) {
                error = $"Destination parent is not editable: {editMessage}";
                return false;
            }
            return true;
        }

        private bool TryGetAddressablesContext(out AddressableAssetGroup group, out string error) {
            group = null;
            error = null;
            if (_settings == null || _addressables == null) {
                error = "Upgrade Tree settings or Addressables settings are missing.";
                return false;
            }
            group = _addressables.FindGroup(_settings.AddressablesGroup);
            if (group == null || group.ReadOnly) {
                error = $"Writable Addressables group '{_settings.AddressablesGroup}' was not found.";
                return false;
            }
            var labels = new HashSet<string>(_addressables.GetLabels(), StringComparer.Ordinal);
            foreach (string label in EnumerateLabels()) {
                if (labels.Contains(label)) continue;
                error = $"Addressables label '{label}' does not exist. Create it in Addressables Groups first.";
                return false;
            }
            return TryRequireAddressablesCleanEditable(group, out error);
        }

        private bool TryRequireAddressablesCleanEditable(AddressableAssetGroup group, out string error) {
            error = null;
            if (_addressables == null || group == null) {
                error = "Addressables settings/group are missing.";
                return false;
            }
            if (EditorUtility.IsDirty(_addressables) || EditorUtility.IsDirty(group)) {
                error = "Addressables settings have unsaved changes. Save or revert them first.";
                return false;
            }
            if (!TryRequireEditable(_addressables, out error) || !TryRequireEditable(group, out error)) return false;
            return true;
        }

        private static bool TryRequireCleanEditable(UnityEngine.Object asset, out string error) {
            error = null;
            if (asset == null) {
                error = "Required asset is missing.";
                return false;
            }
            if (EditorUtility.IsDirty(asset)) {
                error = $"'{AssetDatabase.GetAssetPath(asset)}' has unsaved changes. Save or revert it first.";
                return false;
            }
            return TryRequireEditable(asset, out error);
        }

        private static bool TryRequireEditable(UnityEngine.Object asset, out string error) {
            error = null;
            if (AssetDatabase.CanOpenForEdit(asset, out string message)) return true;
            error = $"'{AssetDatabase.GetAssetPath(asset)}' is not editable: {message}";
            return false;
        }

        private bool TryPreflightOwnedFolder(
            UpgradeNodeDefinition node,
            bool moving,
            out string folder,
            out string[] ownedPaths,
            out string error
        ) {
            folder = null;
            ownedPaths = Array.Empty<string>();
            error = null;
            if (!TryRequireCleanEditable(node, out error)) return false;
            string nodePath = AssetDatabase.GetAssetPath(node);
            folder = GetParentPath(nodePath);
            if (!string.Equals(GetParentPath(folder), _settings.UpgradeRootPath, StringComparison.OrdinalIgnoreCase)) {
                error = "Rename/delete requires a dedicated direct child folder of the configured upgrade root.";
                return false;
            }
            string prefix = folder + "/";
            string[] paths = AssetDatabase.GetAllAssetPaths()
                .Where(path => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (paths.Any(path => AssetDatabase.IsValidFolder(path))) {
                error = "The node folder contains a subfolder and is not exclusively owned by this node.";
                return false;
            }
            var nodeAssets = paths.Where(path => AssetDatabase.LoadAssetAtPath<UpgradeNodeDefinition>(path) != null).ToArray();
            var modifierAssets = paths.Where(path => AssetDatabase.LoadAssetAtPath<ModifierDefinition>(path) != null).ToArray();
            string[] known = nodeAssets.Concat(modifierAssets).ToArray();
            if (nodeAssets.Length != 1 || AssetDatabase.LoadAssetAtPath<UpgradeNodeDefinition>(nodeAssets[0]) != node ||
                known.Length != paths.Length) {
                error = "The node folder contains unknown assets or another upgrade node.";
                return false;
            }
            var referenced = new HashSet<ModifierDefinition>(node.Modifiers ?? Array.Empty<ModifierDefinition>());
            foreach (string modifierPath in modifierAssets) {
                ModifierDefinition modifier = AssetDatabase.LoadAssetAtPath<ModifierDefinition>(modifierPath);
                if (!referenced.Contains(modifier) || GetAllNodes().Any(other => other != node &&
                        (other.Modifiers ?? Array.Empty<ModifierDefinition>()).Contains(modifier))) {
                    error = $"Modifier '{modifierPath}' is unknown or shared by another upgrade.";
                    return false;
                }
                if (!TryRequireCleanEditable(modifier, out error)) return false;
            }
            if (moving && !AssetDatabase.CanOpenForEdit(folder, out string folderMessage)) {
                error = $"Folder is not editable: {folderMessage}";
                return false;
            }
            var knownSet = new HashSet<string>(known, StringComparer.OrdinalIgnoreCase);
            foreach (string assetPath in AssetDatabase.GetAllAssetPaths()) {
                if (AssetDatabase.IsValidFolder(assetPath) || assetPath.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }
                Type type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                if (type != null && typeof(UpgradeNodeDefinition).IsAssignableFrom(type)) continue;
                string[] dependencies = AssetDatabase.GetDependencies(assetPath, false);
                string externalDependency = dependencies.FirstOrDefault(knownSet.Contains);
                if (externalDependency == null) continue;
                error = $"External asset '{assetPath}' depends on '{externalDependency}'.";
                return false;
            }
            ownedPaths = known;
            return true;
        }

        private static bool ContainsCycle(IReadOnlyCollection<UpgradeNodeDefinition> nodes) {
            var selected = new HashSet<UpgradeNodeDefinition>(nodes);
            var visiting = new HashSet<UpgradeNodeDefinition>();
            var visited = new HashSet<UpgradeNodeDefinition>();
            foreach (UpgradeNodeDefinition node in nodes) {
                if (Visit(node)) return true;
            }
            return false;

            bool Visit(UpgradeNodeDefinition current) {
                if (visited.Contains(current)) return false;
                if (!visiting.Add(current)) return true;
                foreach (UpgradeNodeLink link in GetChildren(current)) {
                    if (link.Child != null && selected.Contains(link.Child) && Visit(link.Child)) return true;
                }
                visiting.Remove(current);
                visited.Add(current);
                return false;
            }
        }

        private List<string> FindBillReferences(string id) {
            return AssetDatabase.FindAssets("t:BillRequirementTemplateDefinition")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => {
                    BillRequirementTemplateDefinition definition =
                        AssetDatabase.LoadAssetAtPath<BillRequirementTemplateDefinition>(path);
                    return definition != null && string.Equals(definition.UpgradeId, id, StringComparison.Ordinal);
                }).ToList();
        }

        private static IEnumerable<UpgradeNodeLink> GetChildren(UpgradeNodeDefinition node) {
            return node?.Children ?? Array.Empty<UpgradeNodeLink>();
        }

        private static UpgradeNodeDefinition[] GetAllNodes() {
            return AssetDatabase.FindAssets("t:UpgradeNodeDefinition")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<UpgradeNodeDefinition>)
                .Where(node => node != null)
                .ToArray();
        }

        private IEnumerable<string> EnumerateLabels() {
            yield return AddressableConstants.UPGRADE_LABEL;
            foreach (string label in _settings.ExtraLabels ?? Array.Empty<string>()) {
                if (!string.IsNullOrWhiteSpace(label) && label != AddressableConstants.UPGRADE_LABEL) yield return label;
            }
        }

        private void RestoreEntry(string guid, AddressableAssetGroup group, string address, IEnumerable<string> labels) {
            if (_addressables == null || group == null || string.IsNullOrEmpty(guid)) return;
            AddressableAssetEntry restored = _addressables.CreateOrMoveEntry(guid, group, false, false);
            restored.SetAddress(address, false);
            foreach (string current in restored.labels.ToArray()) restored.SetLabel(current, false, false, false);
            foreach (string label in labels ?? Array.Empty<string>()) restored.SetLabel(label, true, false, false);
        }

        private static void SaveTouched(params UnityEngine.Object[] assets) {
            foreach (UnityEngine.Object asset in assets) {
                if (asset != null) AssetDatabase.SaveAssetIfDirty(asset);
            }
        }

        private static string AllocateAssetPath(string folder, string stem, ISet<string> reserved) {
            int suffix = 1;
            string path = $"{folder}/{stem}.asset";
            while (reserved.Contains(path)) path = $"{folder}/{stem}_{++suffix}.asset";
            reserved.Add(path);
            return path;
        }

        private static bool PathExistsCaseInsensitive(string path) {
            return AssetDatabase.GetAllAssetPaths().Any(existing =>
                UpgradeTreeEditorValidation.PathsEqual(existing, path));
        }

        private static void EnsureFolder(string path) {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = GetParentPath(path);
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, GetName(path)))) {
                throw new InvalidOperationException($"Failed to create folder '{path}'.");
            }
        }

        private static string GetParentPath(string path) {
            int slash = path.LastIndexOf('/');
            return slash <= 0 ? string.Empty : path[..slash];
        }

        private static string GetName(string path) {
            int slash = path.LastIndexOf('/');
            return slash < 0 ? path : path[(slash + 1)..];
        }

        private static string GetGuid(UpgradeNodeDefinition node) {
            return AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(node));
        }
    }
}
