using System;
using Types.Enums;
using Types.Modifiers.Cost.Formula;
using Types.Values;
using UnityEngine;

namespace Types.QTE {
    public enum QteModifierTarget {
        SpawnIntervalSeconds,
        DuplicateSpawnChance,
        IncomeClickMultiplier,
        IncomeClickCritChance,
        IncomeClickCritMultiplier,
        DurationSeconds,
        DurabilityClicks,
        WorkerCount,
        WorkerClickFrequency,
        WorkerIncomeMultiplier,
        WorkerBuildingUpgradeChance
    }

    [Serializable]
    public abstract class QteModifierEffect {
        public ModifierOp Operation = ModifierOp.AddPercent;
        [SerializeReference]
        public IFormula Formula;
        public int Priority;

        public abstract QteModifierTarget Target { get; }

        public bool TryEvaluate(int level, out float value) {
            value = 0f;
            if (Formula == null) return false;

            try {
                value = Formula.Evaluate(new Value(Mathf.Max(0, level))).ToSingle();
                return !float.IsNaN(value) && !float.IsInfinity(value);
            }
            catch {
                value = 0f;
                return false;
            }
        }
    }

    [Serializable]
    public sealed class SpawnIntervalSecondsQteModifierEffect : QteModifierEffect {
        public override QteModifierTarget Target => QteModifierTarget.SpawnIntervalSeconds;
    }

    [Serializable]
    public sealed class DuplicateSpawnChanceQteModifierEffect : QteModifierEffect {
        public override QteModifierTarget Target => QteModifierTarget.DuplicateSpawnChance;
    }

    [Serializable]
    public sealed class IncomeClickMultiplierQteModifierEffect : QteModifierEffect {
        public override QteModifierTarget Target => QteModifierTarget.IncomeClickMultiplier;
    }

    [Serializable]
    public sealed class IncomeClickCritChanceQteModifierEffect : QteModifierEffect {
        public override QteModifierTarget Target => QteModifierTarget.IncomeClickCritChance;
    }

    [Serializable]
    public sealed class IncomeClickCritMultiplierQteModifierEffect : QteModifierEffect {
        public override QteModifierTarget Target => QteModifierTarget.IncomeClickCritMultiplier;
    }

    [Serializable]
    public sealed class DurationSecondsQteModifierEffect : QteModifierEffect {
        public override QteModifierTarget Target => QteModifierTarget.DurationSeconds;
    }

    [Serializable]
    public sealed class DurabilityClicksQteModifierEffect : QteModifierEffect {
        public override QteModifierTarget Target => QteModifierTarget.DurabilityClicks;
    }

    [Serializable]
    public sealed class WorkerCountQteModifierEffect : QteModifierEffect {
        public override QteModifierTarget Target => QteModifierTarget.WorkerCount;
    }

    [Serializable]
    public sealed class WorkerClickFrequencyQteModifierEffect : QteModifierEffect {
        public override QteModifierTarget Target => QteModifierTarget.WorkerClickFrequency;
    }

    [Serializable]
    public sealed class WorkerIncomeMultiplierQteModifierEffect : QteModifierEffect {
        public override QteModifierTarget Target => QteModifierTarget.WorkerIncomeMultiplier;
    }

    [Serializable]
    public sealed class WorkerBuildingUpgradeChanceQteModifierEffect : QteModifierEffect {
        public override QteModifierTarget Target => QteModifierTarget.WorkerBuildingUpgradeChance;
    }

    public struct QteWorkerParameterSnapshot {
        public int Count;
        public float ClickFrequency;
        public float IncomeMultiplier;
        public float BuildingUpgradeChance;
    }
}
