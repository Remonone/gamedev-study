using UnityEditor;
using UnityEngine;

namespace Clicker.Editor.Upgrades {
    internal sealed class UpgradeTreeSettings : ScriptableObject {
        private const string SettingsPath = "Assets/_Clicker/Editor/Upgrades/UpgradeTreeSettings.asset";

        [SerializeField, Min(32f)] private float _nodeSize = 76f;
        [SerializeField, Range(8f, 32f)] private int _idTextSize = 10;
        [SerializeField] private Color _pathColor = new(0.55f, 0.75f, 1f, 0.85f);

        public float NodeSize => Mathf.Clamp(_nodeSize, 32f, 220f);
        public int IdTextSize => Mathf.Clamp(_idTextSize, 8, 32);
        public Color PathColor => _pathColor;

        public static UpgradeTreeSettings GetOrCreate() {
            var settings = AssetDatabase.LoadAssetAtPath<UpgradeTreeSettings>(SettingsPath);
            if (settings != null) return settings;

            settings = CreateInstance<UpgradeTreeSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return settings;
        }

        [MenuItem("Clicker/Upgrade Tree/Settings")]
        private static void OpenSettings() {
            var settings = GetOrCreate();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }
    }
}
