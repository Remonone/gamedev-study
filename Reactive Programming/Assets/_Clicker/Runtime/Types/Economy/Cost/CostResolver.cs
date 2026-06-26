using System;
using System.Collections.Generic;
using Types.Enums;
using Types.Modifiers.Cost.Condition;
using Types.Modifiers.Cost.Formula;
using UnityEngine;

namespace Types.Modifiers.Cost {
    [Serializable]
    public class CostResolver {
        [SerializeField, Tooltip("Cost rows evaluated for the requested level; every passing row contributes one price entry.")]
        private List<CostItem> _costItems;

        public Price Evaluate(int level) {
            Price price = new Price();
            List<Price.Entry> entries = new();
            foreach (var item in _costItems) {
                if (!item.Condition.IsMet(level)) continue;
                entries.Add(new Price.Entry(item.Type, item.Formula.Evaluate(level)));
            }

            price.Entries = entries.ToArray();
            return price;
        }

        [Serializable]
        internal sealed class CostItem {
            [SerializeReference, Tooltip("Level condition that decides whether this cost row is used.")]
            public ILevelCondition Condition;
            [SerializeReference, Tooltip("Formula evaluated with the current level to produce the cost amount.")]
            public IFormula Formula;
            [Tooltip("Resource category paid by this cost row.")]
            public GovernmentInteractionType Type;
        }
    }
    
    
}
