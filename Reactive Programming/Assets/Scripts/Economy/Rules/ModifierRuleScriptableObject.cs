using System.Collections.Generic;
using System.Linq;
using Bases.Buildings;
using Economy.Conditions;
using Types.Economy;
using UnityEngine;

namespace Economy.Rules {
    [CreateAssetMenu(fileName = "ModifierRule", menuName = "Clicker/Economy/Modifier Rule")]
    public sealed class ModifierRule : ScriptableObject {
        [SerializeField] private string ruleId;
        [SerializeField] private ConditionAsset[] conditions;
        [SerializeField] private ModifierDefinition[] modifiers;
        
        public IReadOnlyList<ModifierDefinition> Modifiers => modifiers;

        public bool IsActive(SessionContext context, BuildingState building) {
            if (conditions == null || conditions.Length == 0) return true;

            for (int i = 0; i < conditions.Length; i++) {
                if (conditions[i] == null) continue;
                if (!conditions[i].Evaluate(context, building)) return false;
            }

            return true;
        }

        public void AppendModifiers(BuildingState building, List<StatModifier> output) {
            if (modifiers == null) return;

            output.AddRange(from def in modifiers
            where def.Target.Matches(building)
            select new StatModifier {
                Stat = def.Stat,
                Operation = def.Operation,
                Value = def.Value,
                Priority = def.Priority,
                ModifierId = ruleId,
                Target = def.Target
            });
        }
    }
}
