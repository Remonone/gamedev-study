using System;
using System.Collections.Generic;
using Types;
using Types.Enums;
using Types.QTE;
using Types.Upgrades;
using Types.Upgrades.Effects;

namespace Services.QTE {
    public class QteModifierAggregator : IService {
        
        private readonly IReadOnlyList<Practice> _ownedPractices;
        private readonly IReadOnlyCollection<UpgradeNodeState> _ownedUpgrades;

        public QteModifierAggregator(PracticeService practiceService, UpgradeService upgradeService) {
            _ownedPractices = practiceService.OwnedPracticeDefinitions;
            _ownedUpgrades = upgradeService.OwnedUpgrades;
        }
        
        public float GetModifierBasedValue(float baseValue, QteModifierType type) {
            var flat = 0f;
            var percent = 0f;
            var multiplier = 1f;
            var hasOverride = false;
            var overridePriority = int.MinValue;
            var overrideValue = baseValue;

            ApplyPracticeModifiers(type, ref flat, ref percent, ref multiplier, ref hasOverride, ref overridePriority, ref overrideValue);
            ApplyUpgradeModifiers(type, ref flat, ref percent, ref multiplier, ref hasOverride, ref overridePriority, ref overrideValue);

            if (hasOverride) return overrideValue;
            return ((baseValue + flat) * (1f + percent)) * multiplier;
        }
        
        private void ApplyPracticeModifiers(QteModifierType type, ref float flat, ref float percent, ref float multiplier,
            ref bool hasOverride, ref int overridePriority, ref float overrideValue) {

            for (var i = 0; i < _ownedPractices.Count; i++) {
                var effects = _ownedPractices[i]?.QteEffects;
                ApplyModifierList(effects, type, ref flat, ref percent, ref multiplier, ref hasOverride, ref overridePriority,
                    ref overrideValue);
            }
        }

        private void ApplyUpgradeModifiers(QteModifierType type, ref float flat, ref float percent, ref float multiplier,
            ref bool hasOverride, ref int overridePriority, ref float overrideValue) {

            foreach (var upgrade in _ownedUpgrades) {
                if (upgrade == null || upgrade.Level <= 0 || upgrade.Definition?.Effects == null) continue;

                var effects = upgrade.Definition.Effects;
                for (var effectIndex = 0; effectIndex < effects.Length; effectIndex++) {
                    if (effects[effectIndex] is not QteUpgradeEffect qteEffect) continue;
                    ApplyModifierList(qteEffect.Effects, type, ref flat, ref percent, ref multiplier, ref hasOverride,
                        ref overridePriority, ref overrideValue);
                }
            }
        }
        
        private static void ApplyModifierList(IReadOnlyList<QteModifierEffect> effects, QteModifierType type, ref float flat,
            ref float percent, ref float multiplier, ref bool hasOverride, ref int overridePriority, ref float overrideValue) {
            if (effects == null) return;

            for (var i = 0; i < effects.Count; i++) {
                var effect = effects[i];
                if (effect == null || effect.Type != type) continue;

                switch (effect.Operation) {
                    case ModifierOp.AddFlat:
                        flat += effect.Value;
                        break;
                    case ModifierOp.AddPercent:
                        percent += effect.Value;
                        break;
                    case ModifierOp.Multiply:
                        multiplier *= effect.Value;
                        break;
                    case ModifierOp.Override:
                        if (!hasOverride || effect.Priority > overridePriority) {
                            hasOverride = true;
                            overridePriority = effect.Priority;
                            overrideValue = effect.Value;
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }
}