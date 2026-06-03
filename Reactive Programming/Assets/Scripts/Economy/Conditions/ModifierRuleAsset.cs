using Economy.Rules;
using UnityEngine;

namespace Economy.Conditions {
    [CreateAssetMenu(fileName = "Modifier Asset", menuName = "Clicker/Modifier", order = 0)]
    public class ModifierRuleAsset : ScriptableObject {
        public ConditionAsset ActivationCondition;
        public ModifierDefinition[] Effects;
        public int Priority;
    }
}