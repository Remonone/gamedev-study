using System;
using Data.Formulas;
using Data.Modifiers;
using UnityEngine;

namespace Data.Upgrades {
    public enum LockedNodeDisplayMode {
        VisibleLocked = 0,
        Hidden = 1
    }

    public enum ParentUnlockMode {
        All = 0,
        Any = 1
    }

    public enum StatisticRequirementMode {
        All = 0,
        Any = 1
    }

    public enum StatisticComparison {
        GreaterOrEqual = 0,
        Greater = 1,
        Equal = 2,
        Less = 3,
        LessOrEqual = 4
    }

    [Serializable]
    public struct GameStatisticRequirement {
        public string StatisticId;
        public StatisticComparison Comparison;
        public double TargetValue;
    }

    [Serializable]
    public struct UpgradeNodeLink {
        public UpgradeNodeDefinition Child;
        public bool DrawEdge;
    }

    [CreateAssetMenu(menuName = "Upgrades/Upgrade Node", fileName = "Upgrade Node")]
    public class UpgradeNodeDefinition : ScriptableObject {
        public string Id;
        public string Name;
        [TextArea] public string Description;
        public int MaxLevel;
        public Sprite Icon;
        [SerializeReference] public IFormula CostFormula;
        [SerializeReference] public ModifierDefinition[] Modifiers;
        public string[] FeatureUnlockIds = Array.Empty<string>();

        [Header("Tree")]
        public Vector2 TreePosition;
        public LockedNodeDisplayMode LockedDisplayMode = LockedNodeDisplayMode.VisibleLocked;
        public ParentUnlockMode ParentUnlockMode = ParentUnlockMode.All;
        public StatisticRequirementMode StatisticRequirementMode = StatisticRequirementMode.All;
        public GameStatisticRequirement[] StatisticRequirements = Array.Empty<GameStatisticRequirement>();
        public UpgradeNodeLink[] Children = Array.Empty<UpgradeNodeLink>();

        public bool HasLevelCap => MaxLevel > 0;

        public bool IsTerminalLevel(int level) {
            return level == int.MaxValue || HasLevelCap && level >= MaxLevel;
        }

        public int GetNextPreviewLevel(int level) {
            return IsTerminalLevel(level) ? level : level + 1;
        }
    }
}
