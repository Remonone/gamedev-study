using System;
using System.Collections.Generic;
using Types.Enums.Cost.Condition;
using Types.Enums.Cost.Formula;
using UnityEngine;

namespace Types.Enums.Cost {
    [Serializable]
    public class CostResolver {
        [SerializeField] private List<CostItem> _costItems;

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
            [SerializeReference] public ILevelCondition Condition;
            [SerializeReference] public IFormula Formula;
            public GovernmentInteractionType Type;
        }
    }
    
    
}