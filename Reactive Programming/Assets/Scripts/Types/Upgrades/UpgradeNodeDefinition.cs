using System.Collections.Generic;
using Types.Modifiers.Cost;
using Types.Upgrades.Effects;
using UnityEngine;

namespace Types.Upgrades {
    [CreateAssetMenu(fileName = "Node", menuName = "Clicker/Upgrade Node", order = 0)]
    public class UpgradeNodeDefinition : ScriptableObject {
        public enum Category {Buff, Unlock, Effect}
        
        [SerializeField, Tooltip("Effects applied when this upgrade is purchased.")]
        public UpgradeEffect[] Effects;
        [SerializeField, Tooltip("Upgrade price evaluated with the current upgrade level.")]
        public CostResolver Price;
        [SerializeField, Tooltip("Maximum level this upgrade can reach. Values below 1 are treated as 1.")]
        public int MaxLevel;
        [SerializeField, Tooltip("Position of this node in the upgrade tree UI.")]
        public Vector2 Position;
        [SerializeField, Tooltip("Unique upgrade id used for saves, dependencies and lookups.")]
        public string Id;
        [SerializeField, Tooltip("Display name shown in the upgrade UI.")]
        public string Name;
        [SerializeField, Tooltip("Sprite shown as this upgrade's icon in UI.")]
        public Sprite Icon;
        [SerializeField, TextArea, Tooltip("Description shown in the upgrade UI.")]
        public string Description;
        [SerializeField, Tooltip("How this upgrade is applied: buff, unlock or effect.")]
        public Category NodeCategory;
        [SerializeField, Tooltip("Ids of upgrades that become available after this upgrade is completed.")]
        public List<string> ChildIds;
    }
}
