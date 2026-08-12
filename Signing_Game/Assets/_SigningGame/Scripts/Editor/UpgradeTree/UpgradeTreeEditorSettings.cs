using System;
using UnityEditor;
using UnityEngine;

namespace SigningGame.Editor.UpgradeTree {
    internal sealed class UpgradeTreeEditorSettings : ScriptableObject {
        internal const string AssetPath = "Assets/_SigningGame/Editor/UpgradeTreeEditorSettings.asset";
        internal const string MandatoryLabel = "upgrades";

        [Header("Graph")]
        [Min(24f)] public float NodeSize = 72f;
        [Range(8, 32)] public int IdFontSize = 12;
        [Min(1f)] public float GridSize = 40f;
        public Color NormalPathColor = new(0.3f, 0.8f, 1f, 1f);
        public Color HiddenPathColor = new(0.65f, 0.5f, 0.85f, 1f);
        public Color PhantomPathColor = new(1f, 0.8f, 0.25f, 0.9f);

        [Header("Storage")]
        public string UpgradeRootSuffix = "_SigningGame/Data/Upgrades";

        [Header("Addressables")]
        public string AddressablesGroup = "SigningGame";
        public string[] ExtraLabels = Array.Empty<string>();

        internal string UpgradeRootPath => $"Assets/{UpgradeRootSuffix}";

        internal void ClampValues() {
            NodeSize = Mathf.Clamp(NodeSize, 24f, 240f);
            IdFontSize = Mathf.Clamp(IdFontSize, 8, 32);
            GridSize = Mathf.Clamp(GridSize, 1f, 1000f);
            ExtraLabels ??= Array.Empty<string>();
        }

        internal static UpgradeTreeEditorSettings LoadOrCreate(out string error) {
            error = null;
            var existing = AssetDatabase.LoadAssetAtPath<UpgradeTreeEditorSettings>(AssetPath);
            if (existing != null) {
                return existing;
            }

            const string signingRoot = "Assets/_SigningGame";
            const string editorFolder = "Assets/_SigningGame/Editor";
            if (!AssetDatabase.IsValidFolder(signingRoot)) {
                error = $"Required folder '{signingRoot}' does not exist.";
                return null;
            }

            if (!AssetDatabase.IsValidFolder(editorFolder)) {
                if (!AssetDatabase.CanOpenForEdit(signingRoot, out string editMessage)) {
                    error = $"Cannot create settings folder: {editMessage}";
                    return null;
                }

                string guid = AssetDatabase.CreateFolder(signingRoot, "Editor");
                if (string.IsNullOrEmpty(guid)) {
                    error = $"Failed to create '{editorFolder}'.";
                    return null;
                }
            }

            if (!AssetDatabase.CanOpenForEdit(editorFolder, out string message)) {
                error = $"Cannot create settings asset: {message}";
                return null;
            }

            var settings = CreateInstance<UpgradeTreeEditorSettings>();
            settings.ClampValues();
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssetIfDirty(settings);
            return settings;
        }
    }
}
