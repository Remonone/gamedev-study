using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Types.Modifiers.Definitions;
using Types.Upgrades;
using Types.Upgrades.Effects;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Clicker.Editor.Upgrades {
    internal static class UpgradeTreeAssetUtility {
        public const string UpgradeResourcesFolder = "Assets/Resources/Upgrades";

        public static List<UpgradeNodeDefinition> LoadNodes() {
            EnsureFolder(UpgradeResourcesFolder);
            return Resources.LoadAll<UpgradeNodeDefinition>("Upgrades")
                .Where(node => node != null)
                .ToList();
        }

        public static bool HasDuplicateIds(IReadOnlyList<UpgradeNodeDefinition> nodes, out string message) {
            var duplicates = nodes
                .Where(node => node != null && !string.IsNullOrWhiteSpace(node.Id))
                .GroupBy(node => node.Id)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            if (duplicates.Length == 0) {
                message = string.Empty;
                return false;
            }

            message = "Duplicate upgrade ids found: " + string.Join(", ", duplicates);
            return true;
        }

        public static string GenerateUniqueNodeId(string baseId, IReadOnlyList<UpgradeNodeDefinition> nodes) {
            var safeBase = string.IsNullOrWhiteSpace(baseId) ? "upgrade_node" : SanitizeId(baseId);
            var used = new HashSet<string>(nodes.Where(node => node != null).Select(node => node.Id));
            if (!used.Contains(safeBase)) return safeBase;

            var index = 1;
            string candidate;
            do {
                candidate = $"{safeBase}_{index}";
                index++;
            } while (used.Contains(candidate));

            return candidate;
        }

        public static bool IsIdUnique(string id, UpgradeNodeDefinition self, IReadOnlyList<UpgradeNodeDefinition> nodes) {
            return !string.IsNullOrWhiteSpace(id) && nodes.All(node => node == null || node == self || node.Id != id);
        }

        public static UpgradeNodeDefinition CreateNode(string id, Vector2 position, IReadOnlyList<UpgradeNodeDefinition> loadedNodes) {
            if (HasDuplicateIds(loadedNodes, out _)) return null;

            var uniqueId = GenerateUniqueNodeId(id, loadedNodes);
            var node = ScriptableObject.CreateInstance<UpgradeNodeDefinition>();
            node.Id = uniqueId;
            node.Name = uniqueId;
            node.MaxLevel = 1;
            node.Position = position;
            node.ChildIds = new List<string>();
            node.Effects = Array.Empty<UpgradeEffect>();

            var folder = GetNodeFolder(uniqueId);
            EnsureFolder(folder);
            var path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folder, "Upgrade.asset").Replace('\\', '/'));
            AssetDatabase.CreateAsset(node, path);
            EditorUtility.SetDirty(node);
            AssetDatabase.SaveAssets();
            return node;
        }

        public static UpgradeEffect CreateEffect(UpgradeNodeDefinition node, Type effectType) {
            if (node == null || effectType == null || effectType.IsAbstract || !typeof(UpgradeEffect).IsAssignableFrom(effectType)) {
                return null;
            }

            var effect = (UpgradeEffect)ScriptableObject.CreateInstance(effectType);
            var folder = GetOwnedNodeFolder(node);
            EnsureFolder(folder);
            var path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folder, $"{SanitizeId(node.Id)}_{effectType.Name}.asset").Replace('\\', '/'));
            AssetDatabase.CreateAsset(effect, path);
            EditorUtility.SetDirty(effect);
            AppendEffect(node, effect);
            AssetDatabase.SaveAssets();
            return effect;
        }

        public static ModifierDefinition CreateModifier(ModifierUpgradeEffect effect, string modifierId, Type modifierType) {
            if (effect == null || modifierType == null || modifierType.IsAbstract || !typeof(ModifierDefinition).IsAssignableFrom(modifierType)) {
                return null;
            }

            var modifier = (ModifierDefinition)ScriptableObject.CreateInstance(modifierType);
            var folder = GetModifierFolder(effect);
            EnsureFolder(folder);
            var safeId = SanitizeId(string.IsNullOrWhiteSpace(modifierId) ? modifierType.Name : modifierId);
            var path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folder, $"{safeId}_{modifierType.Name}.asset").Replace('\\', '/'));
            AssetDatabase.CreateAsset(modifier, path);
            SetModifierId(modifier, modifierId);

            Undo.RecordObject(effect, "Add Modifier Definition");
            effect.Rules ??= new List<ModifierDefinition>();
            effect.Rules.Add(modifier);
            EditorUtility.SetDirty(modifier);
            EditorUtility.SetDirty(effect);
            AssetDatabase.SaveAssets();
            Selection.activeObject = modifier;
            return modifier;
        }

        public static List<UpgradeNodeDefinition> CopyNodes(IReadOnlyList<UpgradeNodeDefinition> sources, Vector2 offset, IReadOnlyList<UpgradeNodeDefinition> loadedNodes) {
            var copies = new List<UpgradeNodeDefinition>();
            if (sources == null || sources.Count == 0 || HasDuplicateIds(loadedNodes, out _)) return copies;

            var loadedWithCopies = new List<UpgradeNodeDefinition>(loadedNodes);
            var idMap = new Dictionary<string, string>();
            var sourceToCopy = new Dictionary<UpgradeNodeDefinition, UpgradeNodeDefinition>();

            foreach (var source in sources.Where(node => node != null)) {
                var newId = GenerateCopyId(source.Id, loadedWithCopies);
                var copy = ScriptableObject.CreateInstance<UpgradeNodeDefinition>();
                EditorUtility.CopySerialized(source, copy);
                copy.Id = newId;
                copy.Position = source.Position + offset;
                copy.ChildIds = source.ChildIds == null ? new List<string>() : new List<string>(source.ChildIds);

                var folder = GetNodeFolder(newId);
                EnsureFolder(folder);
                var path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folder, "Upgrade.asset").Replace('\\', '/'));
                AssetDatabase.CreateAsset(copy, path);
                EditorUtility.SetDirty(copy);

                idMap[source.Id] = newId;
                sourceToCopy[source] = copy;
                loadedWithCopies.Add(copy);
                copies.Add(copy);
            }

            foreach (var pair in sourceToCopy) {
                var source = pair.Key;
                var copy = pair.Value;
                copy.ChildIds = RemapChildIds(copy.ChildIds, idMap);
                copy.Effects = CopyOwnedEffects(source, copy);
                EditorUtility.SetDirty(copy);
            }

            AssetDatabase.SaveAssets();
            return copies;
        }

        public static void ApplyNodeIdChange(UpgradeNodeDefinition node, string newId, IReadOnlyList<UpgradeNodeDefinition> nodes) {
            if (node == null || !IsIdUnique(newId, node, nodes)) return;

            var oldId = node.Id;
            Undo.RecordObject(node, "Change Upgrade Node Id");
            node.Id = newId;
            EditorUtility.SetDirty(node);

            foreach (var parent in nodes) {
                if (parent == null || parent.ChildIds == null) continue;

                var changed = false;
                Undo.RecordObject(parent, "Update Upgrade Child Ids");
                for (var i = 0; i < parent.ChildIds.Count; i++) {
                    if (parent.ChildIds[i] == oldId) {
                        parent.ChildIds[i] = newId;
                        changed = true;
                    }
                }

                if (RemoveDuplicateChildIds(parent.ChildIds)) {
                    changed = true;
                }

                if (changed) {
                    EditorUtility.SetDirty(parent);
                }
            }

            AssetDatabase.SaveAssets();
        }

        public static void AppendChild(UpgradeNodeDefinition parent, string childId) {
            if (parent == null || string.IsNullOrWhiteSpace(childId)) return;

            parent.ChildIds ??= new List<string>();
            if (parent.ChildIds.Contains(childId)) return;

            Undo.RecordObject(parent, "Add Upgrade Link");
            parent.ChildIds.Add(childId);
            EditorUtility.SetDirty(parent);
            AssetDatabase.SaveAssets();
        }

        public static bool ToggleChildLink(UpgradeNodeDefinition parent, string childId) {
            if (parent == null || string.IsNullOrWhiteSpace(childId)) return false;

            parent.ChildIds ??= new List<string>();
            Undo.RecordObject(parent, parent.ChildIds.Contains(childId) ? "Remove Upgrade Link" : "Add Upgrade Link");

            var removed = parent.ChildIds.Remove(childId);
            if (!removed) {
                parent.ChildIds.Add(childId);
            }

            EditorUtility.SetDirty(parent);
            AssetDatabase.SaveAssets();
            return !removed;
        }

        public static void DeleteNodes(IReadOnlyList<UpgradeNodeDefinition> nodesToDelete, IReadOnlyList<UpgradeNodeDefinition> allNodes) {
            if (nodesToDelete == null || nodesToDelete.Count == 0) return;

            var deleteSet = new HashSet<UpgradeNodeDefinition>(nodesToDelete.Where(node => node != null));
            if (deleteSet.Count == 0) return;

            var deletedIds = new HashSet<string>(deleteSet
                .Where(node => !string.IsNullOrWhiteSpace(node.Id))
                .Select(node => node.Id));

            foreach (var node in allNodes) {
                if (node == null || deleteSet.Contains(node) || node.ChildIds == null) continue;

                var changed = false;
                Undo.RecordObject(node, "Remove Deleted Upgrade Links");
                for (var i = node.ChildIds.Count - 1; i >= 0; i--) {
                    if (deletedIds.Contains(node.ChildIds[i])) {
                        node.ChildIds.RemoveAt(i);
                        changed = true;
                    }
                }

                if (changed) {
                    EditorUtility.SetDirty(node);
                }
            }

            foreach (var node in deleteSet) {
                var path = AssetDatabase.GetAssetPath(node);
                if (!string.IsNullOrWhiteSpace(path)) {
                    AssetDatabase.DeleteAsset(path);
                }
                else {
                    Object.DestroyImmediate(node, true);
                }
            }

            AssetDatabase.SaveAssets();
        }

        public static string GetOwnedNodeFolder(UpgradeNodeDefinition node) {
            var path = AssetDatabase.GetAssetPath(node);
            if (!string.IsNullOrWhiteSpace(path)) {
                return Path.GetDirectoryName(path)?.Replace('\\', '/') ?? UpgradeResourcesFolder;
            }

            return GetNodeFolder(node != null ? node.Id : "upgrade_node");
        }

        private static void AppendEffect(UpgradeNodeDefinition node, UpgradeEffect effect) {
            Undo.RecordObject(node, "Add Upgrade Effect");
            var effects = node.Effects == null ? new List<UpgradeEffect>() : node.Effects.ToList();
            effects.Add(effect);
            node.Effects = effects.ToArray();
            EditorUtility.SetDirty(node);
        }

        private static UpgradeEffect[] CopyOwnedEffects(UpgradeNodeDefinition source, UpgradeNodeDefinition copy) {
            if (source.Effects == null || source.Effects.Length == 0) return Array.Empty<UpgradeEffect>();

            var sourceFolder = GetOwnedNodeFolder(source);
            var copyFolder = GetOwnedNodeFolder(copy);
            var result = new UpgradeEffect[source.Effects.Length];

            for (var i = 0; i < source.Effects.Length; i++) {
                var effect = source.Effects[i];
                if (effect == null || !IsAssetInsideFolder(effect, sourceFolder)) {
                    result[i] = effect;
                    continue;
                }

                var effectCopy = (UpgradeEffect)ScriptableObject.CreateInstance(effect.GetType());
                EditorUtility.CopySerialized(effect, effectCopy);
                var effectPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(copyFolder, $"{SanitizeId(copy.Id)}_{effect.GetType().Name}.asset").Replace('\\', '/'));
                AssetDatabase.CreateAsset(effectCopy, effectPath);
                EditorUtility.SetDirty(effectCopy);

                if (effect is ModifierUpgradeEffect sourceModifierEffect && effectCopy is ModifierUpgradeEffect modifierEffectCopy) {
                    modifierEffectCopy.Rules = CopyOwnedModifiers(sourceModifierEffect, modifierEffectCopy, sourceFolder, copyFolder);
                    EditorUtility.SetDirty(modifierEffectCopy);
                }

                result[i] = effectCopy;
            }

            return result;
        }

        private static List<ModifierDefinition> CopyOwnedModifiers(ModifierUpgradeEffect sourceEffect, ModifierUpgradeEffect copyEffect, string sourceFolder, string copyFolder) {
            if (sourceEffect.Rules == null) return new List<ModifierDefinition>();

            var result = new List<ModifierDefinition>(sourceEffect.Rules.Count);
            foreach (var modifier in sourceEffect.Rules) {
                if (modifier == null || !IsAssetInsideFolder(modifier, sourceFolder)) {
                    result.Add(modifier);
                    continue;
                }

                var modifierCopy = (ModifierDefinition)ScriptableObject.CreateInstance(modifier.GetType());
                EditorUtility.CopySerialized(modifier, modifierCopy);
                var folder = Path.Combine(copyFolder, "Modifiers").Replace('\\', '/');
                EnsureFolder(folder);
                var path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folder, $"{SanitizeId(copyEffect.name)}_{modifier.GetType().Name}.asset").Replace('\\', '/'));
                AssetDatabase.CreateAsset(modifierCopy, path);
                EditorUtility.SetDirty(modifierCopy);
                result.Add(modifierCopy);
            }

            return result;
        }

        private static bool IsAssetInsideFolder(Object asset, string folder) {
            var path = AssetDatabase.GetAssetPath(asset);
            return !string.IsNullOrWhiteSpace(path) && path.StartsWith(folder.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static List<string> RemapChildIds(List<string> childIds, IReadOnlyDictionary<string, string> idMap) {
            var result = new List<string>();
            if (childIds == null) return result;

            foreach (var childId in childIds) {
                if (string.IsNullOrWhiteSpace(childId)) continue;
                var remapped = idMap.TryGetValue(childId, out var copyId) ? copyId : childId;
                if (!result.Contains(remapped)) {
                    result.Add(remapped);
                }
            }

            return result;
        }

        private static bool RemoveDuplicateChildIds(List<string> childIds) {
            var changed = false;
            var seen = new HashSet<string>();
            for (var i = 0; i < childIds.Count; i++) {
                if (string.IsNullOrWhiteSpace(childIds[i]) || !seen.Add(childIds[i])) {
                    childIds.RemoveAt(i);
                    i--;
                    changed = true;
                }
            }

            return changed;
        }

        private static string GenerateCopyId(string sourceId, IReadOnlyList<UpgradeNodeDefinition> loadedNodes) {
            var baseId = SanitizeId(string.IsNullOrWhiteSpace(sourceId) ? "upgrade_node" : sourceId);
            var used = new HashSet<string>(loadedNodes.Where(node => node != null).Select(node => node.Id));
            var index = 1;
            string candidate;
            do {
                candidate = $"{baseId}_{index}";
                index++;
            } while (used.Contains(candidate));

            return candidate;
        }

        private static string GetNodeFolder(string id) {
            return Path.Combine(UpgradeResourcesFolder, SanitizeId(id)).Replace('\\', '/');
        }

        private static string GetModifierFolder(ModifierUpgradeEffect effect) {
            var effectPath = AssetDatabase.GetAssetPath(effect);
            if (string.IsNullOrWhiteSpace(effectPath)) {
                return Path.Combine(UpgradeResourcesFolder, "SharedModifiers").Replace('\\', '/');
            }

            var effectFolder = Path.GetDirectoryName(effectPath)?.Replace('\\', '/') ?? UpgradeResourcesFolder;
            return Path.Combine(effectFolder, "Modifiers").Replace('\\', '/');
        }

        private static void SetModifierId(ModifierDefinition modifier, string modifierId) {
            if (modifier == null || string.IsNullOrWhiteSpace(modifierId)) return;

            var serializedObject = new SerializedObject(modifier);
            var idProperty = serializedObject.FindProperty("Modifier")?.FindPropertyRelative("ModifierId");
            if (idProperty != null) {
                idProperty.stringValue = modifierId;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static string SanitizeId(string id) {
            if (string.IsNullOrWhiteSpace(id)) return "upgrade_node";

            var builder = new StringBuilder(id.Length);
            foreach (var character in id.Trim()) {
                builder.Append(char.IsLetterOrDigit(character) || character == '_' || character == '-' ? character : '_');
            }

            return builder.Length == 0 ? "upgrade_node" : builder.ToString();
        }

        private static void EnsureFolder(string folder) {
            var normalized = folder.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized)) return;

            var parts = normalized.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++) {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
