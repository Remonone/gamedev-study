using System.Collections.Generic;
using Types.Economy.Cost;
using UnityEngine;

namespace Types.Upgrades {
    [CreateAssetMenu(fileName = "Node", menuName = "Clicker/Upgrade Node", order = 0)]
    public class UpgradeNodeDefinition : ScriptableObject {
        public enum Category {Buff, Unlock, Effect}
        
        [SerializeField] public UpgradeEffect[] Effects;
        [SerializeField] public CostResolver Price;
        [SerializeField] public int MaxLevel;
        [SerializeField] public Vector2 Position;
        [SerializeField] public string Id;
        [SerializeField] public string Name;
        [SerializeField] public Sprite Icon;
        [SerializeField] [TextArea] public string Description;
        [SerializeField] public Category NodeCategory;
        [SerializeField] public List<string> ChildIds;
    }
}