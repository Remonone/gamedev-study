using System;
using Data.Cache;
using Data.Modifiers;
using Data.Modifiers.Calculation;
using Data.Upgrades;
using NUnit.Framework;
using Services;
using Services.Calculators;
using Utils;

namespace Tests.EditMode {
    public sealed class MultiPaySystemTests {
        private static readonly string[] AuthoredNodePaths = {
            "Assets/_SigningGame/Data/Upgrades/upgrade_sign_multi_pay_chance/Upgrade Node.asset",
            "Assets/_SigningGame/Data/Upgrades/office_multi_pay_chance_upgrade/Upgrade Node.asset",
            "Assets/_SigningGame/Data/Upgrades/bank_multi_pay_chance_upgrade/Upgrade Node.asset"
        };

        [Test]
        public void SplitChance_SplitsChanceIntoGuaranteedExtraAndRemainder() {
            Assert.That(MultiPayUtility.SplitChance(0f, out float remainder), Is.Zero);
            Assert.That(remainder, Is.Zero);

            Assert.That(MultiPayUtility.SplitChance(0.24f, out remainder), Is.Zero);
            Assert.That(remainder, Is.EqualTo(0.24f).Within(0.000001f));

            Assert.That(MultiPayUtility.SplitChance(1f, out remainder), Is.EqualTo(1));
            Assert.That(remainder, Is.Zero);

            Assert.That(MultiPayUtility.SplitChance(1.24f, out remainder), Is.EqualTo(1));
            Assert.That(remainder, Is.EqualTo(0.24f).Within(0.000001f));

            Assert.That(MultiPayUtility.SplitChance(2f, out remainder), Is.EqualTo(2));
            Assert.That(remainder, Is.Zero);

            Assert.That(MultiPayUtility.SplitChance(MultiPayUtility.MaximumChance, out remainder),
                Is.EqualTo(100));
            Assert.That(remainder, Is.Zero);

            Assert.That(MultiPayUtility.SplitChance(150f, out remainder), Is.EqualTo(100));
            Assert.That(remainder, Is.Zero);

            Assert.That(MultiPayUtility.SplitChance(-5f, out remainder), Is.Zero);
            Assert.That(remainder, Is.Zero);

            Assert.That(MultiPayUtility.SplitChance(float.NaN, out remainder), Is.Zero);
            Assert.That(remainder, Is.Zero);
        }

        [Test]
        public void BankCacheValidation_RejectsAndNormalizesMultiPayChance() {
            BankEntries invalid = DefaultBankEntries();
            invalid.MultiPayChance = MultiPayUtility.MaximumChance + 1f;
            Assert.Throws<InvalidOperationException>(() => BankCacheCalculator.ValidateBase(invalid));

            invalid = DefaultBankEntries();
            invalid.MultiPayChance = float.NaN;
            Assert.Throws<InvalidOperationException>(() => BankCacheCalculator.ValidateBase(invalid));

            invalid = DefaultBankEntries();
            invalid.MultiPayChance = -1f;
            Assert.Throws<InvalidOperationException>(() => BankCacheCalculator.ValidateBase(invalid));

            BankEntries effective = DefaultBankEntries();
            effective.MultiPayChance = float.NaN;
            BankEntries normalized = BankCacheCalculator.NormalizeEffective(effective);
            Assert.That(normalized.MultiPayChance, Is.Zero);

            effective = DefaultBankEntries();
            effective.MultiPayChance = 500f;
            normalized = BankCacheCalculator.NormalizeEffective(effective);
            Assert.That(normalized.MultiPayChance, Is.EqualTo(MultiPayUtility.MaximumChance));

            effective = DefaultBankEntries();
            effective.MultiPayChance = 1.24f;
            normalized = BankCacheCalculator.NormalizeEffective(effective);
            Assert.That(normalized.MultiPayChance, Is.EqualTo(1.24f).Within(0.000001f));
        }

        [Test]
        public void OfficeCacheValidation_RejectsAndNormalizesMultiPayChance() {
            OfficeEntries invalid = DefaultOfficeEntries();
            invalid.OfficeMultiPayChance = MultiPayUtility.MaximumChance + 1f;
            Assert.Throws<InvalidOperationException>(() => OfficeCacheCalculator.ValidateBase(invalid));

            invalid = DefaultOfficeEntries();
            invalid.OfficeMultiPayChance = float.PositiveInfinity;
            Assert.Throws<InvalidOperationException>(() => OfficeCacheCalculator.ValidateBase(invalid));

            OfficeEntries effective = DefaultOfficeEntries();
            effective.OfficeMultiPayChance = -3f;
            OfficeEntries normalized = OfficeCacheCalculator.NormalizeEffective(effective);
            Assert.That(normalized.OfficeMultiPayChance, Is.Zero);

            effective = DefaultOfficeEntries();
            effective.OfficeMultiPayChance = 42.5f;
            normalized = OfficeCacheCalculator.NormalizeEffective(effective);
            Assert.That(normalized.OfficeMultiPayChance, Is.EqualTo(42.5f).Within(0.000001f));
        }

        [Test]
        public void AuthoredMultiPayUpgrades_TargetRegisteredParametersAndScalePerLevel() {
            foreach (string path in AuthoredNodePaths) {
                UpgradeNodeDefinition node = UnityEditor.AssetDatabase
                    .LoadAssetAtPath<UpgradeNodeDefinition>(path);
                Assert.That(node, Is.Not.Null, $"Missing authored upgrade node at {path}");
                Assert.That(node.MaxLevel, Is.EqualTo(20), $"{node.Id} level cap");
                Assert.That(node.Modifiers, Is.Not.Null.And.Length.EqualTo(1),
                    $"{node.Id} must reference exactly one modifier definition");
                Assert.That(node.Modifiers[0].NumericModifiers, Is.Not.Null.And.Count.EqualTo(1),
                    $"{node.Id} modifier must contain exactly one numeric modifier");

                NumericModifierDefinition modifier = node.Modifiers[0].NumericModifiers[0];
                Assert.DoesNotThrow(() => modifier.ValidateConfiguration(),
                    $"{node.Id} modifier targets an unregistered group/parameter");

                IModifierContext context = new ModifierContext()
                    .Add(new LevelModifierCapability(20))
                    .Add(new ModifierEffectivenessCapability(1f));
                Assert.That(modifier.EvaluateAtLevel(20).ToDouble(), Is.EqualTo(2d).Within(0.0001d),
                    $"{node.Id} must add +10% per level for 20 levels");
                float appliedChance = ResolveChance(modifier.Apply(ResolveDefaultEntries(modifier), context));
                Assert.That(appliedChance, Is.EqualTo(2f).Within(0.0001f),
                    $"{node.Id} must apply +200% at max level");
            }
        }

        [Test]
        public void MultiPayModifiers_AreClampedToMaximumChance() {
            foreach (string path in AuthoredNodePaths) {
                UpgradeNodeDefinition node = UnityEditor.AssetDatabase
                    .LoadAssetAtPath<UpgradeNodeDefinition>(path);
                Assert.That(node, Is.Not.Null, $"Missing authored upgrade node at {path}");
                NumericModifierDefinition modifier = node.Modifiers[0].NumericModifiers[0];

                IModifierContext context = new ModifierContext()
                    .Add(new LevelModifierCapability(5000))
                    .Add(new ModifierEffectivenessCapability(1f));
                object entries = ResolveDefaultEntries(modifier);
                object modified = modifier.Apply(entries, context);
                Assert.That(ResolveChance(modified), Is.EqualTo(MultiPayUtility.MaximumChance),
                    $"{node.Id} effective chance must clamp at {MultiPayUtility.MaximumChance}");
            }
        }

        [Test]
        public void AuthoredParameterIds_MatchEntryFieldNames() {
            // Guards the hand-authored YAML against typos: every authored parameter id
            // must resolve to a registered modifiable field on its entries struct.
            AssertAuthoredParameter("upgrade_sign_multi_pay_chance",
                "Income", nameof(IncomeEntries.ManualSignatureMultiPayChance));
            AssertAuthoredParameter("office_multi_pay_chance_upgrade",
                "Office", nameof(OfficeEntries.OfficeMultiPayChance));
            AssertAuthoredParameter("bank_multi_pay_chance_upgrade",
                "Bank", nameof(BankEntries.MultiPayChance));
        }

        private static void AssertAuthoredParameter(string upgradeId, string expectedGroup, string expectedParameter) {
            UpgradeNodeDefinition node = null;
            foreach (string path in AuthoredNodePaths) {
                UpgradeNodeDefinition candidate = UnityEditor.AssetDatabase
                    .LoadAssetAtPath<UpgradeNodeDefinition>(path);
                if (candidate != null && candidate.Id == upgradeId) {
                    node = candidate;
                    break;
                }
            }

            Assert.That(node, Is.Not.Null, $"Authored upgrade '{upgradeId}' was not found");
            NumericModifierDefinition modifier = node.Modifiers[0].NumericModifiers[0];
            Assert.That(modifier.ParameterGroupId, Is.EqualTo(expectedGroup), $"{upgradeId} group id");
            Assert.That(modifier.ParameterId, Is.EqualTo(expectedParameter), $"{upgradeId} parameter id");
            Assert.That(modifier.Operation, Is.EqualTo(NumericModifierOperation.Add),
                $"{upgradeId} operation");
        }

        private static object ResolveDefaultEntries(NumericModifierDefinition modifier) {
            string groupId = modifier.ParameterGroupId;
            if (groupId == "Income") return new IncomeEntries(1f, 0.4f, Value.One);
            if (groupId == "Office") return DefaultOfficeEntries();
            if (groupId == "Bank") return DefaultBankEntries();
            throw new AssertionException($"Unknown authored group '{groupId}'");
        }

        private static float ResolveChance(object entries) {
            return entries switch {
                IncomeEntries income => income.ManualSignatureMultiPayChance,
                OfficeEntries office => office.OfficeMultiPayChance,
                BankEntries bank => bank.MultiPayChance,
                _ => throw new AssertionException($"Unknown entries type '{entries.GetType().Name}'")
            };
        }

        private static BankEntries DefaultBankEntries() {
            return new BankEntries {
                PayoutAmount = Value.One,
                PayoutIntervalSeconds = 10f,
                CriticalChance = 0f,
                CriticalMultiplier = 2d,
                BillCostCompensationRatio = 0d,
                MultiPayChance = 0f
            };
        }

        private static OfficeEntries DefaultOfficeEntries() {
            return new OfficeEntries {
                ClerkCapacity = 1,
                DocumentsPerSecondPerClerk = 1f,
                QualityCeiling = 1f,
                AcceptanceThreshold = 0.5f,
                RewardMultiplier = 0.5f,
                OfficeSignatureCriticalChance = 0f,
                OfficeSignatureCriticalMultiplier = 1d,
                OfficeMultiPayChance = 0f,
                BaseClerkMultiplierMedian = 2d,
                ClerkMultiplierRangeStep = 0d,
                MinimumClerkMultiplier = 1d,
                MaximumHireSignatureMultiplier = 2d,
                SalaryReviewCostRatio = 0.5d
            };
        }
    }
}
