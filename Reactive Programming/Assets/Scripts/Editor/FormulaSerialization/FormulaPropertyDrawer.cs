using FormulaSerialization.UIElements;
using Types.Economy.Cost.Formula;
using UnityEditor;
using UnityEngine.UIElements;

namespace FormulaSerialization {
    [CustomPropertyDrawer(typeof(IFormula), true)]
    public sealed class FormulaPropertyDrawer : PropertyDrawer {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            return new FormulaField(property);
        }
    }
}
