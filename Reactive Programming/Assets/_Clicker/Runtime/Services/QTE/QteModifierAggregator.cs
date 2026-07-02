using System;
using System.Collections.Generic;
using Types;
using Types.Enums;
using Types.QTE;
using Types.Upgrades;
using Types.Upgrades.Effects;
using UnityEngine;

namespace Services.QTE {
    public class QteModifierAggregator : IService {
        private readonly PracticeService _practiceService;
        private readonly UpgradeService _upgradeService;

        public QteModifierAggregator(PracticeService practiceService, UpgradeService upgradeService) {
            _practiceService = practiceService;
            _upgradeService = upgradeService;
        }

        public float ResolveSpawnIntervalSeconds(float baseValue) => Mathf.Max(0.1f, ResolveValue(baseValue, QteModifierTarget.SpawnIntervalSeconds, true, true));
        public float ResolveDuplicateSpawnChance(float baseValue = 0f) => Mathf.Max(0f, ResolveValue(baseValue, QteModifierTarget.DuplicateSpawnChance, true, true));
        public float ResolveIncomeClickMultiplier(float baseValue = 1f) => Mathf.Max(0f, ResolveValue(baseValue, QteModifierTarget.IncomeClickMultiplier, true, true));
        public float ResolveIncomeClickCritChance(float baseValue = 0f) => Mathf.Clamp01(ResolveValue(baseValue, QteModifierTarget.IncomeClickCritChance, true, true));
        public float ResolveIncomeClickCritMultiplier(float baseValue = 1f) => Mathf.Max(1f, ResolveValue(baseValue, QteModifierTarget.IncomeClickCritMultiplier, true, true));
        public float ResolveDurationSeconds(float baseValue) => Mathf.Max(0.01f, ResolveValue(baseValue, QteModifierTarget.DurationSeconds, true, true));
        public int ResolveDurabilityClicks(int baseValue) => Mathf.Max(1, Mathf.RoundToInt(ResolveValue(baseValue, QteModifierTarget.DurabilityClicks, true, true)));
        public int ResolveWorkerCount(float baseValue = 0f) => Mathf.Max(0, Mathf.RoundToInt(ResolveValue(baseValue, QteModifierTarget.WorkerCount, true, true)));
        public float ResolveWorkerClickFrequency(float baseValue = 0f) => Mathf.Max(0f, ResolveValue(baseValue, QteModifierTarget.WorkerClickFrequency, true, true));
        public float ResolveWorkerIncomeMultiplier(float baseValue = 1f) => Mathf.Max(0f, ResolveValue(baseValue, QteModifierTarget.WorkerIncomeMultiplier, true, true));
        public float ResolveWorkerBuildingUpgradeChance(float baseValue = 0.0001f) => Mathf.Clamp01(ResolveValue(baseValue, QteModifierTarget.WorkerBuildingUpgradeChance, true, true));

        public QteWorkerParameterSnapshot ResolveWorkerSnapshot() {
            return new QteWorkerParameterSnapshot {
                Count = ResolveWorkerCount(),
                ClickFrequency = ResolveWorkerClickFrequency(),
                IncomeMultiplier = ResolveWorkerIncomeMultiplier(),
                BuildingUpgradeChance = ResolveWorkerBuildingUpgradeChance()
            };
        }

        private float ResolveValue(float baseValue, QteModifierTarget target, bool includePractices, bool includeUpgrades) {
            var flat = 0f;
            var percent = 0f;
            var multiplier = 1f;
            var hasOverride = false;
            var overridePriority = int.MinValue;
            var overrideValue = baseValue;

            if (includePractices) {
                ApplyPracticeModifiers(target, ref flat, ref percent, ref multiplier, ref hasOverride, ref overridePriority, ref overrideValue);
            }

            if (includeUpgrades) {
                ApplyUpgradeModifiers(target, ref flat, ref percent, ref multiplier, ref hasOverride, ref overridePriority, ref overrideValue);
            }

            return Sanitize(hasOverride ? overrideValue : ((baseValue + flat) * (1f + percent)) * multiplier, baseValue);
        }

        private void ApplyPracticeModifiers(QteModifierTarget target, ref float flat, ref float percent, ref float multiplier,
            ref bool hasOverride, ref int overridePriority, ref float overrideValue) {

            var ownedPractices = _practiceService?.OwnedPracticeDefinitions;
            if (ownedPractices == null) return;

            for (var i = 0; i < ownedPractices.Count; i++) {
                ApplyModifierList(ownedPractices[i]?.QteImprovements, target, 1, ref flat, ref percent, ref multiplier,
                    ref hasOverride, ref overridePriority, ref overrideValue);
            }
        }

        private void ApplyUpgradeModifiers(QteModifierTarget target, ref float flat,
            ref float percent, ref float multiplier, ref bool hasOverride, ref int overridePriority, ref float overrideValue) {

            var ownedUpgrades = _upgradeService?.OwnedUpgrades;
            if (ownedUpgrades == null) return;

            foreach (var upgrade in ownedUpgrades) {
                if (upgrade == null || upgrade.Level <= 0 || upgrade.Definition?.Effects == null) continue;

                var effects = upgrade.Definition.Effects;
                for (var effectIndex = 0; effectIndex < effects.Length; effectIndex++) {
                    if (effects[effectIndex] is not QteUpgradeEffect qteEffect) continue;
                    ApplyModifierList(qteEffect.Effects, target, upgrade.Level, ref flat, ref percent, ref multiplier,
                        ref hasOverride, ref overridePriority, ref overrideValue);
                }
            }
        }

        private static void ApplyModifierList(IReadOnlyList<QteModifierEffect> modifiers, QteModifierTarget target, int level, ref float flat,
            ref float percent, ref float multiplier, ref bool hasOverride, ref int overridePriority, ref float overrideValue) {
            if (modifiers == null) return;

            for (var i = 0; i < modifiers.Count; i++) {
                var effect = modifiers[i];
                if (effect == null || effect.Target != target || !effect.TryEvaluate(level, out var value)) continue;

                switch (effect.Operation) {
                    case ModifierOp.AddFlat:
                        flat += value;
                        break;
                    case ModifierOp.AddPercent:
                        percent += value;
                        break;
                    case ModifierOp.Multiply:
                        multiplier *= value;
                        break;
                    case ModifierOp.Override:
                        if (!hasOverride || effect.Priority > overridePriority) {
                            hasOverride = true;
                            overridePriority = effect.Priority;
                            overrideValue = value;
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private static float Sanitize(float value, float fallback) {
            if (IsFinite(value)) return value;
            return IsFinite(fallback) ? fallback : 0f;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
