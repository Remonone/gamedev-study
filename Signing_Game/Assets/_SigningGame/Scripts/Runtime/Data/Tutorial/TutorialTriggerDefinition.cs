using System;
using Data.Upgrades;
using Services;

namespace Data.Tutorial {
    /// <summary>
    /// Comparison options for <see cref="StatisticsTrigger"/> checks against a statistics row value.
    /// </summary>
    public enum TutorialStatisticComparison {
        GreaterOrEqual = 0,
        Greater = 1,
        Equal = 2,
        NotEqual = 3,
        Less = 4,
        LessOrEqual = 5
    }

    /// <summary>
    /// Context handed to trigger evaluations. Built and owned by <see cref="TutorialService"/>.
    /// </summary>
    public sealed class TutorialTriggerContext {
        public GameStatisticsService Statistics { get; }
        public UpgradeService Upgrades { get; }

        public TutorialTriggerContext(GameStatisticsService statistics, UpgradeService upgrades) {
            Statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
            Upgrades = upgrades ?? throw new ArgumentNullException(nameof(upgrades));
        }
    }

    [Serializable]
    public abstract class TutorialTriggerDefinition {
        public abstract bool IsSatisfied(TutorialTriggerContext context);
    }

    /// <summary>
    /// Watches a single statistics row. A missing statistic row never satisfies the trigger
    /// (including <see cref="TutorialStatisticComparison.NotEqual"/>).
    /// </summary>
    [Serializable]
    public sealed class StatisticsTrigger : TutorialTriggerDefinition {
        public string StatisticId;
        public TutorialStatisticComparison Comparison = TutorialStatisticComparison.GreaterOrEqual;
        public double TargetValue;

        public override bool IsSatisfied(TutorialTriggerContext context) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (string.IsNullOrWhiteSpace(StatisticId)) return false;
            if (!context.Statistics.TryGetValue(StatisticId, out double value)) return false;

            return Comparison switch {
                TutorialStatisticComparison.GreaterOrEqual => value >= TargetValue,
                TutorialStatisticComparison.Greater => value > TargetValue,
                TutorialStatisticComparison.Equal => value.Equals(TargetValue),
                TutorialStatisticComparison.NotEqual => !value.Equals(TargetValue),
                TutorialStatisticComparison.Less => value < TargetValue,
                TutorialStatisticComparison.LessOrEqual => value <= TargetValue,
                _ => false
            };
        }
    }

    /// <summary>
    /// Watches whether an upgrade has been purchased/leveled up to <see cref="MinLevel"/>.
    /// </summary>
    [Serializable]
    public sealed class UpgradeTrigger : TutorialTriggerDefinition {
        public string UpgradeId;
        [UnityEngine.Tooltip("Required upgrade level. 1 means 'purchased'.")]
        public int MinLevel = 1;

        public override bool IsSatisfied(TutorialTriggerContext context) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (string.IsNullOrWhiteSpace(UpgradeId) || MinLevel < 0) return false;

            UpgradeNodeState upgrade = context.Upgrades.GetUpgrade(UpgradeId);
            return upgrade != null && upgrade.Level >= MinLevel;
        }
    }
}
