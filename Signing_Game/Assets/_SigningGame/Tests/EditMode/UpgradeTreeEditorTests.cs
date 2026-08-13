using System;
using System.Collections.Generic;
using System.Linq;
using Data.Formulas;
using Data.Modifiers;
using Data.Modifiers.Numeric;
using Data.Upgrades;
using NUnit.Framework;
using SigningGame.Editor.Modifiers;
using SigningGame.Editor.UpgradeTree;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace SigningGame.Tests.EditMode {
    public sealed class UpgradeTreeEditorTests {
        private const string _Root = "Assets/__UpgradeTreeEditorTests";
        private const string _UpgradeRoot = _Root + "/Upgrades";
        private const string _AddressablesRoot = _Root + "/Addressables";

        private UpgradeTreeEditorSettings _settings;
        private AddressableAssetSettings _addressables;
        private AddressableAssetGroup _group;
        private UpgradeTreeEditorOperations _operations;
        private AddressableAssetSettings _defaultAddressables;
        private AddressableAssetGroup[] _defaultGroups;

        [SetUp]
        public void SetUp() {
            _defaultAddressables = AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(EditorUtility.IsDirty(_defaultAddressables), Is.False,
                "Default Addressables settings must be saved before isolated editor tests run.");
            _defaultGroups = _defaultAddressables.groups.ToArray();
            AssetDatabase.DeleteAsset(_Root);
            AssetDatabase.CreateFolder("Assets", "__UpgradeTreeEditorTests");
            AssetDatabase.CreateFolder(_Root, "Upgrades");
            AssetDatabase.CreateFolder(_Root, "Addressables");

            _settings = ScriptableObject.CreateInstance<UpgradeTreeEditorSettings>();
            _settings.UpgradeRootSuffix = "__UpgradeTreeEditorTests/Upgrades";
            _settings.AddressablesGroup = "Test Upgrades";
            _settings.ExtraLabels = Array.Empty<string>();
            AssetDatabase.CreateAsset(_settings, _Root + "/Settings.asset");

            _addressables = AddressableAssetSettings.Create(_AddressablesRoot, "TestAddressables", false, true);
            _addressables.AddLabel(UpgradeTreeEditorSettings.MandatoryLabel, false);
            _group = _addressables.CreateGroup(
                _settings.AddressablesGroup,
                true,
                false,
                false,
                null,
                typeof(BundledAssetGroupSchema),
                typeof(ContentUpdateGroupSchema));
            AssetDatabase.SaveAssetIfDirty(_addressables);
            AssetDatabase.SaveAssetIfDirty(_group);
            AssetDatabase.SaveAssetIfDirty(_settings);
            _operations = new UpgradeTreeEditorOperations(_settings, _addressables);
            UpgradeTreeEditorOperations.TestFaultPoint = UpgradeTreeFaultPoint.None;
        }

        [Test]
        public void NewNumericModifier_DoesNotReusePreviousManagedValue() {
            var modifier = ScriptableObject.CreateInstance<ModifierDefinition>();
            modifier.NumericModifiers = new List<NumericModifierDefinition> { new() };
            try {
                var serialized = new SerializedObject(modifier);
                SerializedProperty modifiers = serialized.FindProperty(nameof(ModifierDefinition.NumericModifiers));
                modifiers.GetArrayElementAtIndex(0).FindPropertyRelative("_value").managedReferenceValue =
                    new ConstantNumericValueDefinition();
                serialized.ApplyModifiedPropertiesWithoutUndo();

                serialized.Update();
                modifiers.InsertArrayElementAtIndex(1);
                UpgradeTreeEditorWindow.InitializeNewNumericModifiers(modifiers, 1);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                serialized.Update();
                Assert.That(modifiers.GetArrayElementAtIndex(0).FindPropertyRelative("_value").managedReferenceValue,
                    Is.Not.Null);
                Assert.That(modifiers.GetArrayElementAtIndex(1).FindPropertyRelative("_value").managedReferenceValue,
                    Is.Null);
            } finally {
                UnityEngine.Object.DestroyImmediate(modifier);
            }
        }

        [Test]
        public void NumericValueDrawer_DetachesSharedManagedValueReference() {
            var modifier = ScriptableObject.CreateInstance<ModifierDefinition>();
            modifier.NumericModifiers = new List<NumericModifierDefinition> { new(), new() };
            try {
                var serialized = new SerializedObject(modifier);
                SerializedProperty modifiers = serialized.FindProperty(nameof(ModifierDefinition.NumericModifiers));
                var sharedValue = new ConstantNumericValueDefinition();
                SerializedProperty firstValue = modifiers.GetArrayElementAtIndex(0).FindPropertyRelative("_value");
                SerializedProperty secondValue = modifiers.GetArrayElementAtIndex(1).FindPropertyRelative("_value");
                firstValue.managedReferenceValue = sharedValue;
                secondValue.managedReferenceValue = sharedValue;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                serialized.Update();

                modifiers = serialized.FindProperty(nameof(ModifierDefinition.NumericModifiers));
                secondValue = modifiers.GetArrayElementAtIndex(1).FindPropertyRelative("_value");
                Assert.That(NumericValueDefinitionPropertyDrawer.EnsureUniqueArrayReference(secondValue), Is.True);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                serialized.Update();
                modifiers = serialized.FindProperty(nameof(ModifierDefinition.NumericModifiers));
                firstValue = modifiers.GetArrayElementAtIndex(0).FindPropertyRelative("_value");
                secondValue = modifiers.GetArrayElementAtIndex(1).FindPropertyRelative("_value");

                Assert.That(firstValue.managedReferenceValue, Is.Not.Null);
                Assert.That(secondValue.managedReferenceValue, Is.Not.Null);
                Assert.That(secondValue.managedReferenceId, Is.Not.EqualTo(firstValue.managedReferenceId));
            } finally {
                UnityEngine.Object.DestroyImmediate(modifier);
            }
        }

        [TearDown]
        public void TearDown() {
            UpgradeTreeEditorOperations.TestFaultPoint = UpgradeTreeFaultPoint.None;
            RestoreDefaultAddressablesGroups();
            AssetDatabase.DeleteAsset(_Root);
            Assert.That(AssetDatabase.IsValidFolder(_Root), Is.False);
        }

        [TestCase("")]
        [TestCase(" ")]
        [TestCase(".")]
        [TestCase("..")]
        [TestCase("bad:name")]
        [TestCase("bad/name")]
        [TestCase("bad\\name")]
        [TestCase("bad[name]")]
        [TestCase("bad.")]
        [TestCase("CON")]
        [TestCase("con.txt")]
        [TestCase("LPT9")]
        public void SegmentValidation_RejectsCrossPlatformInvalidNames(string value) {
            Assert.That(UpgradeTreeEditorValidation.TryValidateSegment(value, "Value", out _), Is.False);
        }

        [Test]
        public void RootValidation_AllowsCanonicalSuffix_AndRejectsTraversal() {
            Assert.That(UpgradeTreeEditorValidation.TryValidateRootSuffix("_SigningGame/Data/Upgrades", out string path,
                out _), Is.True);
            Assert.That(path, Is.EqualTo("_SigningGame/Data/Upgrades"));
            Assert.That(UpgradeTreeEditorValidation.TryValidateRootSuffix("_SigningGame/../Upgrades", out _, out _),
                Is.False);
            Assert.That(UpgradeTreeEditorValidation.TryValidateRootSuffix("Assets/_SigningGame", out _, out _), Is.False);
        }

        [Test]
        public void CreateAndRename_RejectAddressablesBracketCharacters() {
            Assert.That(_operations.CreateNode("bad[node]", Vector2.zero).Success, Is.False);
            UpgradeNodeDefinition node = Create("safe_node");
            Assert.That(_operations.RenameNode(node, "bad[node]").Success, Is.False);
        }

        [Test]
        public void CreateNode_PersistsDefaultsAndMandatoryAddressablesLabel() {
            UpgradeTreeOperationResult<UpgradeNodeDefinition> result = _operations.CreateNode("first", new Vector2(40f, 80f));

            Assert.That(result.Success, Is.True, result.Error);
            string path = AssetDatabase.GetAssetPath(result.Value);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            UpgradeNodeDefinition loaded = AssetDatabase.LoadAssetAtPath<UpgradeNodeDefinition>(path);
            Assert.That(loaded.Id, Is.EqualTo("first"));
            Assert.That(loaded.TreePosition, Is.EqualTo(new Vector2(40f, 80f)));
            Assert.That(loaded.CostFormula, Is.TypeOf<ConstantValue>());
            Assert.That(loaded.Children, Is.Empty);
            AddressableAssetEntry entry = _addressables.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.labels, Does.Contain(UpgradeTreeEditorSettings.MandatoryLabel));
            Assert.That(entry.address, Is.EqualTo(path));
        }

        [Test]
        public void CreateNode_FaultRollsBackFolderAndEntry() {
            UpgradeTreeEditorOperations.TestFaultPoint = UpgradeTreeFaultPoint.AfterPersistBeforeVerification;

            UpgradeTreeOperationResult<UpgradeNodeDefinition> result = _operations.CreateNode("faulted", Vector2.zero);

            Assert.That(result.Success, Is.False);
            Assert.That(AssetDatabase.IsValidFolder(_UpgradeRoot + "/faulted"), Is.False);
            Assert.That(_addressables.groups.SelectMany(group => group.entries).Any(entry => entry.address.Contains("faulted")),
                Is.False);
        }

        [Test]
        public void Edges_RejectDuplicatesAndCycles_AndHiddenEdgeParticipates() {
            UpgradeNodeDefinition a = Create("a");
            UpgradeNodeDefinition b = Create("b");
            UpgradeNodeDefinition c = Create("c");

            Assert.That(_operations.AddEdge(a, b, false).Success, Is.True);
            Assert.That(_operations.AddEdge(a, b, false).Success, Is.False);
            Assert.That(_operations.AddEdge(b, c).Success, Is.True);
            Assert.That(_operations.AddEdge(c, a).Success, Is.False);
            Assert.That(a.Children.Single().DrawEdge, Is.False);
            Assert.That(_operations.RemoveEdge(a, b).Success, Is.True);
            Assert.That(a.Children, Is.Empty);
        }

        [Test]
        public void LegacyNullCollections_AreToleratedAndNormalizedOnlyOnMutation() {
            UpgradeNodeDefinition a = Create("legacy_a");
            UpgradeNodeDefinition b = Create("legacy_b");
            a.Children = null;
            a.Modifiers = null;
            EditorUtility.SetDirty(a);
            AssetDatabase.SaveAssetIfDirty(a);

            Assert.That(_operations.WouldCreateCycle(a, b), Is.False);
            Assert.That(_operations.AddEdge(a, b).Success, Is.True);
            Assert.That(a.Children, Has.Length.EqualTo(1));
        }

        [Test]
        public void CopyNodes_RemapsHiddenInternalLinksAndPreservesExternalLinks() {
            UpgradeNodeDefinition a = Create("copy_a");
            UpgradeNodeDefinition b = Create("copy_b");
            UpgradeNodeDefinition external = Create("external");
            Assert.That(_operations.AddEdge(a, b, false).Success, Is.True);
            Assert.That(_operations.AddEdge(a, external, true).Success, Is.True);

            UpgradeTreeOperationResult<IReadOnlyList<UpgradeNodeDefinition>> result =
                _operations.CopyNodes(new[] { a, b }, new Vector2(40f, 40f));

            Assert.That(result.Success, Is.True, result.Error);
            UpgradeNodeDefinition aCopy = result.Value.Single(node => node.Id == "copy_a_1");
            UpgradeNodeDefinition bCopy = result.Value.Single(node => node.Id == "copy_b_1");
            Assert.That(aCopy.Children.Any(link => link.Child == bCopy && !link.DrawEdge), Is.True);
            Assert.That(aCopy.Children.Any(link => link.Child == external && link.DrawEdge), Is.True);
        }

        [Test]
        public void CopyNodes_CopiesSharedModifierOnce() {
            UpgradeNodeDefinition a = Create("modifier_a");
            UpgradeNodeDefinition b = Create("modifier_b");
            UpgradeTreeOperationResult<ModifierDefinition> modifierResult = _operations.CreateModifier(a, "Shared Modifier");
            Assert.That(modifierResult.Success, Is.True, modifierResult.Error);
            Assert.That(_operations.SetModifierReference(b, 0, modifierResult.Value).Success, Is.True);

            UpgradeTreeOperationResult<IReadOnlyList<UpgradeNodeDefinition>> result =
                _operations.CopyNodes(new[] { a, b }, Vector2.one * 40f);

            Assert.That(result.Success, Is.True, result.Error);
            Assert.That(result.Value[0].Modifiers.Single(), Is.SameAs(result.Value[1].Modifiers.Single()));
            Assert.That(result.Value[0].Modifiers.Single(), Is.Not.SameAs(modifierResult.Value));
        }

        [Test]
        public void RenameNode_UpdatesIdFolderAndAddress_AndFaultRestores() {
            UpgradeNodeDefinition node = Create("old_id");

            UpgradeTreeOperationResult<UpgradeNodeDefinition> renamed = _operations.RenameNode(node, "new_id");
            Assert.That(renamed.Success, Is.True, renamed.Error);
            Assert.That(AssetDatabase.GetAssetPath(renamed.Value), Does.StartWith(_UpgradeRoot + "/new_id/"));
            Assert.That(_addressables.FindAssetEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(renamed.Value))).address,
                Is.EqualTo(AssetDatabase.GetAssetPath(renamed.Value)));

            UpgradeTreeEditorOperations.TestFaultPoint = UpgradeTreeFaultPoint.AfterMoveAndPersistBeforeVerification;
            UpgradeTreeOperationResult<UpgradeNodeDefinition> fault = _operations.RenameNode(renamed.Value, "fault_id");
            Assert.That(fault.Success, Is.False);
            UpgradeNodeDefinition restored = AssetDatabase.LoadAssetAtPath<UpgradeNodeDefinition>(
                _UpgradeRoot + "/new_id/Upgrade Node.asset");
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.Id, Is.EqualTo("new_id"));
        }

        [Test]
        public void RenameNode_RefusesDirtyNode() {
            UpgradeNodeDefinition node = Create("dirty");
            node.Name = "Unsaved";
            EditorUtility.SetDirty(node);

            UpgradeTreeOperationResult<UpgradeNodeDefinition> result = _operations.RenameNode(node, "clean");

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Does.Contain("unsaved"));
        }

        [Test]
        public void DeleteNode_RemovesInboundHiddenLinkAndFolder() {
            UpgradeNodeDefinition parent = Create("parent");
            UpgradeNodeDefinition child = Create("child");
            Assert.That(_operations.AddEdge(parent, child, false).Success, Is.True);

            UpgradeTreeOperationResult<bool> result = _operations.DeleteNode(child);

            Assert.That(result.Success, Is.True, result.Error);
            Assert.That(parent.Children, Is.Empty);
            Assert.That(AssetDatabase.IsValidFolder(_UpgradeRoot + "/child"), Is.False);
        }

        [Test]
        public void DeleteNode_FaultRestoresInboundHiddenLinkAndEntry() {
            UpgradeNodeDefinition parent = Create("fault_parent");
            UpgradeNodeDefinition child = Create("fault_child");
            Assert.That(_operations.AddEdge(parent, child, false).Success, Is.True);
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(child));
            UpgradeTreeEditorOperations.TestFaultPoint = UpgradeTreeFaultPoint.AfterStagingPersistedBeforeFolderDelete;

            UpgradeTreeOperationResult<bool> result = _operations.DeleteNode(child);

            Assert.That(result.Success, Is.False);
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(parent), ImportAssetOptions.ForceUpdate);
            Assert.That(parent.Children.Single().Child, Is.SameAs(child));
            Assert.That(parent.Children.Single().DrawEdge, Is.False);
            Assert.That(_addressables.FindAssetEntry(guid), Is.Not.Null);
            Assert.That(AssetDatabase.IsValidFolder(_UpgradeRoot + "/fault_child"), Is.True);
        }

        [TestCase(0, false)]
        [TestCase(1, true)]
        [TestCase(2, false)]
        public void DeleteSelection_RequiresExactlyOneNode(int count, bool expected) {
            Assert.That(UpgradeTreeEditorOperations.CanDeleteSelection(count), Is.EqualTo(expected));
        }

        private UpgradeNodeDefinition Create(string id) {
            UpgradeTreeOperationResult<UpgradeNodeDefinition> result = _operations.CreateNode(id, Vector2.zero);
            Assert.That(result.Success, Is.True, result.Error);
            return result.Value;
        }

        private void RestoreDefaultAddressablesGroups() {
            if (_defaultAddressables == null || _defaultGroups == null) return;
            bool changed = _defaultAddressables.groups.Count != _defaultGroups.Length;
            if (!changed) {
                for (var index = 0; index < _defaultGroups.Length; index++) {
                    if (_defaultAddressables.groups[index] == _defaultGroups[index]) continue;
                    changed = true;
                    break;
                }
            }
            if (!changed) return;
            _defaultAddressables.groups.Clear();
            _defaultAddressables.groups.AddRange(_defaultGroups);
            EditorUtility.SetDirty(_defaultAddressables);
            AssetDatabase.SaveAssetIfDirty(_defaultAddressables);
        }
    }
}
