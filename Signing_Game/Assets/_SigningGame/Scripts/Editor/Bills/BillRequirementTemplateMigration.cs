using System;
using Data.Bills;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Compilation;
using UnityEngine;

namespace SigningGame.Editor.Bills {
    [InitializeOnLoad]
    internal static class BillRequirementTemplateMigration {
        static BillRequirementTemplateMigration() {
            EditorApplication.delayCall += MigrateLegacyTemplates;
        }

        [DidReloadScripts]
        private static void MigrateAfterCompilation() {
            EditorApplication.delayCall += MigrateLegacyTemplates;
        }

        private static void MigrateLegacyTemplates() {
            EditorApplication.delayCall -= MigrateLegacyTemplates;
            string[] guids = AssetDatabase.FindAssets("t:BillRequirementTemplateDefinition");
            bool changed = false;
            for (int index = 0; index < guids.Length; index++) {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                BillRequirementTemplateDefinition asset =
                    AssetDatabase.LoadAssetAtPath<BillRequirementTemplateDefinition>(path);
                if (asset == null) continue;

                var serialized = new SerializedObject(asset);
                SerializedProperty definition = serialized.FindProperty("Definition");
                if (definition == null || definition.managedReferenceValue != null) continue;

                SerializedProperty kindProperty = serialized.FindProperty("_legacyKind") ??
                                                  serialized.FindProperty("Kind");
                if (kindProperty == null || kindProperty.propertyType != SerializedPropertyType.Integer) continue;
                int kind = kindProperty.intValue;
                BillRequirementDefinition migrated = CreateDefinition(serialized, kind);
                if (migrated == null) continue;

                definition.managedReferenceValue = migrated;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                changed = true;
            }

            if (changed) AssetDatabase.SaveAssets();
        }

        private static BillRequirementDefinition CreateDefinition(SerializedObject serialized, int kind) {
            int minimum = ReadInt(serialized, "_legacyMinimumTarget", "MinimumTarget");
            int maximum = ReadInt(serialized, "_legacyMaximumTarget", "MaximumTarget");
            string upgradeId = ReadString(serialized, "_legacyUpgradeId", "UpgradeId");
            return kind switch {
                (int)BillRequirementKind.OwnedUpgrade => new OwnedUpgradeRequirementDefinition {
                    UpgradeId = upgradeId
                },
                (int)BillRequirementKind.MinimumClerkCount => new MinimumClerkCountRequirementDefinition {
                    MinimumTarget = minimum,
                    MaximumTarget = maximum
                },
                (int)BillRequirementKind.MinimumUnlockedDocumentQuality =>
                    new MinimumDocumentQualityRequirementDefinition {
                        MinimumTarget = minimum,
                        MaximumTarget = maximum
                    },
                _ => null
            };
        }

        private static int ReadInt(SerializedObject serialized, string preferred, string legacy) {
            SerializedProperty property = serialized.FindProperty(preferred) ?? serialized.FindProperty(legacy);
            return property?.propertyType == SerializedPropertyType.Integer ? property.intValue : 0;
        }

        private static string ReadString(SerializedObject serialized, string preferred, string legacy) {
            SerializedProperty property = serialized.FindProperty(preferred) ?? serialized.FindProperty(legacy);
            return property?.propertyType == SerializedPropertyType.String ? property.stringValue : string.Empty;
        }
    }
}
