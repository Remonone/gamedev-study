using LevelConditionSerialization.UIElements;
using Types.Modifiers.Definitions.Cost.Condition;
using UnityEditor;
using UnityEngine.UIElements;

namespace LevelConditionSerialization {
    [CustomPropertyDrawer(typeof(ILevelCondition), true)]
    public sealed class LevelConditionPropertyDrawer : PropertyDrawer {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            return new LevelConditionField(property);
        }
    }
}
