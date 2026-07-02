using Types.Upgrades.Effects;
using UnityEditor;

namespace Clicker.Editor.Upgrades {
    [CustomEditor(typeof(QteUpgradeEffect))]
    public sealed class QteUpgradeEffectEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("Effects"), true);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
