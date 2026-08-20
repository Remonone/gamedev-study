using Data.Bills;
using UnityEditor;
using SigningGame.Editor.Tutorial;

namespace SigningGame.Editor.Bills {
    [CustomPropertyDrawer(typeof(BillRequirementDefinition), true)]
    internal sealed class BillRequirementDefinitionDrawer :
        SerializableDefinitionSelectorDrawer<BillRequirementDefinition> { }
}
