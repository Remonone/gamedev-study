using System;
using System.Collections.Generic;
using Types.Economy.Cost.Condition;
using Types.Economy.Cost.Formula;
using UnityEngine;

namespace Types.Economy.Cost {
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
            public StructureType Type;
        }
    }
    
    
}