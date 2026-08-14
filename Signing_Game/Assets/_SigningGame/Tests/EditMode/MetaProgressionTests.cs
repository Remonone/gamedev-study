using System.Collections.Generic;
using Data.Formulas;
using Data.Upgrades;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Services;
using Services.Locator;
using UnityEngine;
using Utils;

namespace Tests.EditMode {
    public sealed class MetaProgressionTests {
        private readonly List<Object> _assets = new();

        [TearDown]
        public void TearDown() {
            for (int index = _assets.Count - 1; index >= 0; index--) Object.DestroyImmediate(_assets[index]);
            _assets.Clear();
        }

        [Test]
        public void MoneyFormula_UsesStoredAndDegreeExactlyAsSpecified() {
            Assert.That(MetaPointCalculator.FromMoneyPeak(Value.Zero), Is.Zero);
            Assert.That(MetaPointCalculator.FromMoneyPeak(new Value(92.2d, new BaseValue(2))), Is.EqualTo(5));
            Assert.That(MetaPointCalculator.Calculate(3, new Value(92.2d, new BaseValue(2))), Is.EqualTo(8));
            Assert.That(MetaPointCalculator.FromMoneyPeak(new Value(999d)), Is.EqualTo(3));
            Assert.That(MetaPointCalculator.FromMoneyPeak(new Value(1d, new BaseValue(1))), Is.EqualTo(1));
            Assert.That(MetaPointCalculator.FromMoneyPeak(new Value(1d, new BaseValue(500))), Is.EqualTo(1498));
        }

        [TestCase(4, 0, false)]
        [TestCase(5, 0, true)]
        [TestCase(5, 5, false)]
        [TestCase(6, 5, true)]
        [TestCase(5, 6, false)]
        public void Eligibility_RequiresThresholdAndGrowth(long current, long previous, bool expected) {
            Assert.That(MetaPointCalculator.IsEligible(current, previous), Is.EqualTo(expected));
        }

        [Test]
        public void MarkedOrdinaryUpgrade_ContributesOnePointPerOwnedLevel() {
            UpgradeNodeDefinition marked = CreateOrdinaryNode("marked", 1d);
            marked.GrantsMetaCurrencyPoint = true;
            UpgradeNodeDefinition unmarked = CreateOrdinaryNode("unmarked", 1d);
            var states = new[] {
                new UpgradeNodeState(marked, 3, UpgradeNodeState.State.InProgress),
                new UpgradeNodeState(unmarked, 9, UpgradeNodeState.State.InProgress)
            };

            Assert.That(MetaPointCalculator.CountMarkedLevels(states), Is.EqualTo(3));
        }

        [Test]
        public void PurchasePreview_BanksCurrentPointsAndResetsIterationState() {
            MetaUpgradeNodeDefinition node = CreateMetaNode("meta_income", 5d);
            using var meta = new MetaProgressionService();
            meta.BuildDefinitions(new[] { node });
            meta.Deserialize(CreateState(0, 0, new Value(92.2d, new BaseValue(2))));

            Assert.That(meta.CurrentIterationPoints, Is.EqualTo(5));
            Assert.That(meta.IsEligible, Is.True);
            Assert.That(meta.TryCreatePurchasedState(node.Id, out JToken purchased, out long cost), Is.True);
            Assert.That(cost, Is.EqualTo(5));
            Assert.That(purchased["bankedPoints"]?.Value<long>(), Is.Zero);
            Assert.That(purchased["previousIterationPoints"]?.Value<long>(), Is.EqualTo(5));
            Assert.That(purchased["moneyPeakStored"]?.Value<double>(), Is.Zero);
            Assert.That(purchased["upgrades"]?[0]?["id"]?.Value<string>(), Is.EqualTo(node.Id));
            Assert.That(purchased["upgrades"]?[0]?["level"]?.Value<int>(), Is.EqualTo(1));
        }

        [Test]
        public void UnavailableCatalog_PreservesUnresolvedOwnershipAndDisablesPurchases() {
            using var meta = new MetaProgressionService();
            meta.BuildDefinitions(System.Array.Empty<MetaUpgradeNodeDefinition>());
            JObject state = CreateState(7, 5, Value.Zero);
            ((JArray)state["upgrades"]).Add(new JObject { ["id"] = "missing", ["level"] = 3 });

            meta.Deserialize(state);
            JToken serialized = meta.Serialize();

            Assert.That(meta.IsCatalogAvailable, Is.False);
            Assert.That(serialized["upgrades"]?[0]?["id"]?.Value<string>(), Is.EqualTo("missing"));
            Assert.That(serialized["upgrades"]?[0]?["level"]?.Value<int>(), Is.EqualTo(3));
            Assert.That(meta.CanPurchase("missing"), Is.False);
        }

        [Test]
        public void MalformedRestore_IsAtomic() {
            MetaUpgradeNodeDefinition node = CreateMetaNode("meta", 5d);
            using var meta = new MetaProgressionService();
            meta.BuildDefinitions(new[] { node });
            meta.Deserialize(CreateState(9, 5, new Value(10d)));
            JToken before = meta.Serialize();

            Assert.Throws<JsonSerializationException>(() => meta.Deserialize(new JObject {
                ["bankedPoints"] = -1,
                ["previousIterationPoints"] = 0,
                ["moneyPeakStored"] = 0,
                ["moneyPeakDegree"] = 0,
                ["upgrades"] = new JArray()
            }));

            Assert.That(JToken.DeepEquals(meta.Serialize(), before), Is.True);
        }

        [Test]
        public void CycleNodes_KeepRestoredEffectsButCannotBePurchased() {
            MetaUpgradeNodeDefinition first = CreateMetaNode("first", 1d);
            MetaUpgradeNodeDefinition second = CreateMetaNode("second", 1d);
            first.Children = new[] { new UpgradeNodeLink { Child = second, DrawEdge = true } };
            second.Children = new[] { new UpgradeNodeLink { Child = first, DrawEdge = true } };
            using var meta = new MetaProgressionService();
            meta.BuildDefinitions(new[] { first, second });
            JObject state = CreateState(10, 0, new Value(92.2d, new BaseValue(2)));
            ((JArray)state["upgrades"]).Add(new JObject { ["id"] = first.Id, ["level"] = 1 });
            meta.Deserialize(state);
            using var scope = new ServiceScope(null);
            using var tree = new MetaUpgradeTreeService();
            scope.Register(meta).Register(tree);
            tree.InitializeAsync(scope).GetAwaiter().GetResult();

            Assert.That(meta.GetUpgrade(first.Id).Level, Is.EqualTo(1));
            Assert.That(meta.OwnedMetaUpgrades, Has.Count.EqualTo(1));
            Assert.That(tree.IsConfigurationValid(first.Id), Is.False);
            Assert.That(tree.CanPurchase(first.Id), Is.False);
            Assert.That(tree.CanPurchase(second.Id), Is.False);
        }

        [Test]
        public void RuntimeCatalogs_ExcludeCrossLabeledTypesAndCrossTreeLinks() {
            MetaUpgradeNodeDefinition metaNode = CreateMetaNode("meta", 1d);
            UpgradeNodeDefinition ordinaryNode = CreateOrdinaryNode("ordinary", 1d);
            using var ordinary = new UpgradeService();
            ordinary.BuildDefinitions(new UpgradeNodeDefinition[] { metaNode });
            Assert.That(ordinary.Nodes, Is.Empty);

            metaNode.Children = new[] { new UpgradeNodeLink { Child = ordinaryNode, DrawEdge = true } };
            using var meta = new MetaProgressionService();
            meta.BuildDefinitions(new[] { metaNode });
            meta.Deserialize(CreateState(10, 0, new Value(92.2d, new BaseValue(2))));
            using var scope = new ServiceScope(null);
            using var tree = new MetaUpgradeTreeService();
            scope.Register(meta).Register(tree);
            tree.InitializeAsync(scope).GetAwaiter().GetResult();
            Assert.That(tree.IsConfigurationValid(metaNode.Id), Is.False);
        }

        private MetaUpgradeNodeDefinition CreateMetaNode(string id, double cost) {
            var node = Track(ScriptableObject.CreateInstance<MetaUpgradeNodeDefinition>());
            Configure(node, id, cost);
            return node;
        }

        private UpgradeNodeDefinition CreateOrdinaryNode(string id, double cost) {
            var node = Track(ScriptableObject.CreateInstance<UpgradeNodeDefinition>());
            Configure(node, id, cost);
            return node;
        }

        private static void Configure(UpgradeNodeDefinition node, string id, double cost) {
            node.Id = id;
            node.Name = id;
            node.MaxLevel = 2;
            node.CostFormula = new ConstantValue { Value = new Value(cost) };
            node.Modifiers = System.Array.Empty<Data.Modifiers.ModifierDefinition>();
            node.Children = System.Array.Empty<UpgradeNodeLink>();
            node.FeatureUnlockIds = System.Array.Empty<string>();
            node.StatisticRequirements = System.Array.Empty<GameStatisticRequirement>();
        }

        private T Track<T>(T asset) where T : Object {
            _assets.Add(asset);
            return asset;
        }

        private static JObject CreateState(long banked, long previous, Value peak) {
            return new JObject {
                ["bankedPoints"] = banked,
                ["previousIterationPoints"] = previous,
                ["moneyPeakStored"] = peak.Stored,
                ["moneyPeakDegree"] = peak.Base.Degree,
                ["upgrades"] = new JArray()
            };
        }
    }
}
