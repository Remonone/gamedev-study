using System;
using System.Collections.Generic;
using System.Reflection;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Formulas;
using Data.Modifiers;
using Data.Enums;
using Data.Results;
using Data.Upgrades;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Presentation;
using R3;
using Services;
using Services.Locator;
using UI;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;
using Utils;
using Utils.Metadata;

namespace Tests.EditMode {
    public sealed class UpgradeTreeSystemTests {
        private readonly List<UpgradeNodeDefinition> _definitions = new();
        private readonly List<ModifierDefinition> _modifierDefinitions = new();
        private readonly List<IDisposable> _disposables = new();
        private WalletService _lastWallet;

        [Test]
        public void EdgeGraphic_UsesBezierTangentsForForwardReverseAndVerticalConnections() {
            UpgradeEdgeGraphic.CalculateBezierControls(
                Vector2.zero,
                new Vector2(120f, 60f),
                80f,
                out Vector2 forwardFirst,
                out Vector2 forwardSecond);
            Assert.That(forwardFirst, Is.EqualTo(new Vector2(60f, 0f)));
            Assert.That(forwardSecond, Is.EqualTo(new Vector2(60f, 60f)));

            UpgradeEdgeGraphic.CalculateBezierControls(
                new Vector2(120f, 10f),
                new Vector2(0f, 50f),
                80f,
                out Vector2 reverseFirst,
                out Vector2 reverseSecond);
            Assert.That(reverseFirst, Is.EqualTo(new Vector2(60f, 10f)));
            Assert.That(reverseSecond, Is.EqualTo(new Vector2(60f, 50f)));

            UpgradeEdgeGraphic.CalculateBezierControls(
                new Vector2(10f, 0f),
                new Vector2(50f, 200f),
                120f,
                out Vector2 verticalFirst,
                out Vector2 verticalSecond);
            Assert.That(verticalFirst, Is.EqualTo(new Vector2(10f, 100f)));
            Assert.That(verticalSecond, Is.EqualTo(new Vector2(50f, 100f)));
        }

        [TearDown]
        public void TearDown() {
            for (int index = _disposables.Count - 1; index >= 0; index--) _disposables[index].Dispose();
            _disposables.Clear();
            for (int index = 0; index < _definitions.Count; index++) {
                UnityEngine.Object.DestroyImmediate(_definitions[index]);
            }
            _definitions.Clear();
            for (int index = 0; index < _modifierDefinitions.Count; index++) {
                UnityEngine.Object.DestroyImmediate(_modifierDefinitions[index]);
            }
            _modifierDefinitions.Clear();
        }

        [Test]
        public void GameStatistics_ChangesOnceAndRestoresAtomically() {
            var statistics = Track(new GameStatisticsService());
            int notifications = 0;
            using IDisposable subscription = statistics.Changed.Subscribe(_ => notifications++);

            Assert.That(statistics.SetValue("signed_documents", 3d), Is.True);
            Assert.That(statistics.SetValue("signed_documents", 3d), Is.False);
            Assert.That(notifications, Is.EqualTo(1));

            statistics.Deserialize(new JObject { ["signed_documents"] = 8d, ["office_level"] = 2d });
            Assert.That(notifications, Is.EqualTo(2));
            Assert.That(statistics.TryGetValue("signed_documents", out double restored), Is.True);
            Assert.That(restored, Is.EqualTo(8d));

            Assert.Throws<JsonSerializationException>(() => statistics.Deserialize(new JObject {
                ["signed_documents"] = double.NaN
            }));
            Assert.That(statistics.TryGetValue("signed_documents", out double unchanged), Is.True);
            Assert.That(unchanged, Is.EqualTo(8d));
            Assert.That(notifications, Is.EqualTo(2));
        }

        [Test]
        public void Tree_EvaluatesAnyAndAllParents() {
            UpgradeNodeDefinition first = CreateDefinition("first");
            UpgradeNodeDefinition second = CreateDefinition("second");
            UpgradeNodeDefinition child = CreateDefinition("child");
            first.Children = new[] { new UpgradeNodeLink { Child = child, DrawEdge = true } };
            second.Children = new[] { new UpgradeNodeLink { Child = child, DrawEdge = false } };
            child.ParentUnlockMode = ParentUnlockMode.Any;

            SetupTree(new[] { first, second, child }, out UpgradeService upgrades,
                out GameStatisticsService statistics, out UpgradeTreeService tree);
            Assert.That(tree.IsUnlocked("child"), Is.False);

            PurchaseAndComplete(upgrades, "first");
            Assert.That(tree.IsUnlocked("child"), Is.True);

            child.ParentUnlockMode = ParentUnlockMode.All;
            tree.Reevaluate();
            Assert.That(tree.IsUnlocked("child"), Is.False);

            PurchaseAndComplete(upgrades, "second");
            Assert.That(tree.IsUnlocked("child"), Is.True);
            Assert.That(statistics, Is.Not.Null);
        }

        [Test]
        public void Tree_UsesStatisticsAndHiddenModeAndFiltersEdges() {
            UpgradeNodeDefinition parent = CreateDefinition("parent");
            UpgradeNodeDefinition child = CreateDefinition("child");
            parent.Children = new[] { new UpgradeNodeLink { Child = child, DrawEdge = true } };
            child.LockedDisplayMode = LockedNodeDisplayMode.Hidden;
            child.StatisticRequirements = new[] {
                new GameStatisticRequirement {
                    StatisticId = "documents",
                    Comparison = StatisticComparison.GreaterOrEqual,
                    TargetValue = 5d
                }
            };

            SetupTree(new[] { parent, child }, out UpgradeService upgrades,
                out GameStatisticsService statistics, out UpgradeTreeService tree);
            PurchaseAndComplete(upgrades, "parent");
            Assert.That(tree.IsUnlocked("child"), Is.False);
            Assert.That(tree.IsVisible("child"), Is.False);

            using var viewModel = new UpgradeTreeViewModel(tree, upgrades, _lastWallet);
            Assert.That(viewModel.Edges, Is.Empty);

            statistics.SetValue("documents", 5d);
            Assert.That(tree.IsUnlocked("child"), Is.True);
            Assert.That(tree.IsVisible("child"), Is.True);
            Assert.That(viewModel.Edges.Count, Is.EqualTo(1));
        }

        [Test]
        public void Tree_HidesVisibleLockedDescendantWhenParentIsHidden() {
            UpgradeNodeDefinition root = CreateDefinition("root");
            UpgradeNodeDefinition hiddenParent = CreateDefinition("hidden_parent");
            UpgradeNodeDefinition visibleChild = CreateDefinition("visible_child");
            root.Children = new[] { new UpgradeNodeLink { Child = hiddenParent, DrawEdge = true } };
            hiddenParent.Children = new[] { new UpgradeNodeLink { Child = visibleChild, DrawEdge = true } };
            hiddenParent.LockedDisplayMode = LockedNodeDisplayMode.Hidden;
            visibleChild.LockedDisplayMode = LockedNodeDisplayMode.VisibleLocked;

            SetupTree(new[] { root, hiddenParent, visibleChild }, out UpgradeService upgrades,
                out _, out UpgradeTreeService tree);

            Assert.That(tree.IsVisible("hidden_parent"), Is.False);
            Assert.That(tree.IsVisible("visible_child"), Is.False);

            PurchaseAndComplete(upgrades, "root");

            Assert.That(tree.IsVisible("hidden_parent"), Is.True);
            Assert.That(tree.IsVisible("visible_child"), Is.True);
        }

        [Test]
        public void Selection_CanSelectSameNodeAgainAfterClear() {
            UpgradeNodeDefinition definition = CreateDefinition("selectable");
            SetupTree(new[] { definition }, out UpgradeService upgrades, out _, out UpgradeTreeService tree);
            using var viewModel = new UpgradeTreeViewModel(tree, upgrades, _lastWallet);
            var selectedIds = new List<string>();
            using IDisposable subscription = viewModel.SelectedNode.Subscribe(node => selectedIds.Add(node?.Id));

            viewModel.SelectNode("selectable");
            viewModel.ClearSelection();
            viewModel.SelectNode("selectable");

            Assert.That(selectedIds, Is.EqualTo(new string[] { null, "selectable", null, "selectable" }));
        }

        [Test]
        public void Presentation_InvertsTreeYForRuntimeRectTransformCoordinates() {
            UpgradeNodeDefinition parent = CreateDefinition("parent");
            UpgradeNodeDefinition child = CreateDefinition("child");
            parent.TreePosition = new Vector2(20f, 100f);
            child.TreePosition = new Vector2(20f, 0f);
            parent.Children = new[] { new UpgradeNodeLink { Child = child, DrawEdge = true } };
            SetupTree(new[] { parent, child }, out UpgradeService upgrades, out _, out UpgradeTreeService tree);
            using var viewModel = new UpgradeTreeViewModel(tree, upgrades, _lastWallet);

            UpgradeNodePresentationModel parentModel = FindNode(viewModel.Nodes, parent.Id);
            UpgradeNodePresentationModel childModel = FindNode(viewModel.Nodes, child.Id);

            Assert.That(parentModel.Position, Is.EqualTo(new Vector2(20f, -100f)));
            Assert.That(childModel.Position, Is.EqualTo(new Vector2(20f, 0f)));
            Assert.That(childModel.Position.y, Is.GreaterThan(parentModel.Position.y));
            Assert.That(viewModel.Edges[0].Start, Is.EqualTo(parentModel.Position));
            Assert.That(viewModel.Edges[0].End, Is.EqualTo(childModel.Position));
        }

        [Test]
        public void Tree_EvaluatesAnyAndAllStatisticRequirements() {
            UpgradeNodeDefinition node = CreateDefinition("statistics");
            node.StatisticRequirementMode = StatisticRequirementMode.Any;
            node.StatisticRequirements = new[] {
                new GameStatisticRequirement {
                    StatisticId = "first",
                    Comparison = StatisticComparison.GreaterOrEqual,
                    TargetValue = 1d
                },
                new GameStatisticRequirement {
                    StatisticId = "second",
                    Comparison = StatisticComparison.Greater,
                    TargetValue = 2d
                }
            };

            SetupTree(new[] { node }, out _, out GameStatisticsService statistics, out UpgradeTreeService tree);
            Assert.That(tree.IsUnlocked("statistics"), Is.False);

            statistics.SetValue("second", 3d);
            Assert.That(tree.IsUnlocked("statistics"), Is.True);

            node.StatisticRequirementMode = StatisticRequirementMode.All;
            tree.Reevaluate();
            Assert.That(tree.IsUnlocked("statistics"), Is.False);

            statistics.SetValue("first", 1d);
            Assert.That(tree.IsUnlocked("statistics"), Is.True);
        }

        [Test]
        public void Tree_InvalidEnumsFailClosedAndInvalidDisplayFallsBackVisible() {
            UpgradeNodeDefinition node = CreateDefinition("invalid");
            node.ParentUnlockMode = (ParentUnlockMode)99;
            node.StatisticRequirementMode = (StatisticRequirementMode)99;
            node.LockedDisplayMode = (LockedNodeDisplayMode)99;
            LogAssert.Expect(LogType.Warning, "Upgrade 'invalid' has an invalid parent unlock mode and will stay locked.");
            LogAssert.Expect(LogType.Warning,
                "Upgrade 'invalid' has an invalid statistic requirement mode and will stay locked.");
            LogAssert.Expect(LogType.Warning,
                "Upgrade 'invalid' has an invalid locked display mode; VisibleLocked will be used.");

            SetupTree(new[] { node }, out _, out _, out UpgradeTreeService tree);

            Assert.That(tree.IsUnlocked("invalid"), Is.False);
            Assert.That(tree.IsVisible("invalid"), Is.True);
        }

        [Test]
        public void Tree_InvalidStatisticRequirementFailsClosed() {
            UpgradeNodeDefinition node = CreateDefinition("invalid_stat");
            node.StatisticRequirements = new[] {
                new GameStatisticRequirement {
                    StatisticId = "",
                    Comparison = (StatisticComparison)99,
                    TargetValue = double.NaN
                }
            };
            LogAssert.Expect(LogType.Warning,
                "Upgrade 'invalid_stat' has an invalid statistic requirement at index 0 and will stay locked.");

            SetupTree(new[] { node }, out _, out _, out UpgradeTreeService tree);

            Assert.That(tree.IsUnlocked("invalid_stat"), Is.False);
        }

        [Test]
        public void Graph_InvalidLinksAreExcludedFromParentsAndPresentation() {
            UpgradeNodeDefinition parent = CreateDefinition("parent");
            UpgradeNodeDefinition child = CreateDefinition("child");
            UpgradeNodeDefinition unloaded = CreateDefinition("unloaded");
            parent.Children = new[] {
                new UpgradeNodeLink { Child = null, DrawEdge = true },
                new UpgradeNodeLink { Child = parent, DrawEdge = true },
                new UpgradeNodeLink { Child = unloaded, DrawEdge = true },
                new UpgradeNodeLink { Child = child, DrawEdge = true },
                new UpgradeNodeLink { Child = child, DrawEdge = true }
            };
            LogAssert.Expect(LogType.Warning, "Upgrade 'parent' child link at index 0 has no child definition; the link was excluded.");
            LogAssert.Expect(LogType.Warning, "Upgrade 'parent' child link at index 1 is a self-link; the link was excluded.");
            LogAssert.Expect(LogType.Warning,
                "Upgrade 'parent' child link at index 2 references unloaded child 'unloaded'; the link was excluded.");
            LogAssert.Expect(LogType.Warning,
                "Upgrade 'parent' child link at index 4 duplicates child 'child'; the link was excluded.");

            SetupTree(new[] { parent, child }, out _, out _, out UpgradeTreeService tree);

            Assert.That(tree.Connections.Count, Is.EqualTo(1));
            Assert.That(tree.IsUnlocked("child"), Is.False);
        }

        [Test]
        public void TryUpgradeAndCompletion_ReplaceCatalogBeforeNotificationsAndTreeNotifiesAfterSource() {
            UpgradeNodeDefinition definition = CreateDefinition("upgrade", maxLevel: 2);
            SetupTree(new[] { definition }, out UpgradeService upgrades, out _, out UpgradeTreeService tree,
                subscribeBeforeTree: (service, events) => service.Changed.Subscribe(_ => {
                    UpgradeNodeState current = service.GetUpgrade("upgrade");
                    events.Add(current.CurrentState == UpgradeNodeState.State.Pending
                        ? "upgrade-pending"
                        : "upgrade-complete");
                }));

            var order = _notificationOrder;
            using IDisposable treeSubscription = tree.Changed.Subscribe(_ => order.Add("tree"));
            UpgradeNodeState before = upgrades.GetUpgrade("upgrade");

            Assert.That(upgrades.TryUpgrade("upgrade"), Is.True);
            Assert.That(upgrades.GetUpgrade("upgrade").Level, Is.Zero);
            Assert.That(upgrades.GetUpgrade("upgrade").CurrentState, Is.EqualTo(UpgradeNodeState.State.Pending));
            CompletePending(upgrades);

            Assert.That(upgrades.GetUpgrade("upgrade"), Is.Not.SameAs(before));
            Assert.That(upgrades.GetUpgrade("upgrade").Level, Is.EqualTo(1));
            Assert.That(order, Is.EqualTo(new[] {
                "upgrade-pending", "tree", "upgrade-complete", "tree"
            }));
        }

        [Test]
        public void UnlimitedUpgradePresentation_UsesInfinityAndNeverTreatsZeroAsAuthoredMaximum() {
            UpgradeNodeDefinition definition = CreateDefinition("unlimited", maxLevel: 0);
            SetupTree(new[] { definition }, out UpgradeService upgrades, out _, out UpgradeTreeService tree);
            using var viewModel = new UpgradeTreeViewModel(tree, upgrades, _lastWallet);

            UpgradeNodePresentationModel node = viewModel.Nodes[0];
            Assert.That(node.LevelText, Is.EqualTo("0/∞"));
            Assert.That(node.Price, Is.Not.EqualTo("MAX"));
            Assert.That(node.CanPurchase, Is.True);
        }

        [Test]
        public void AvailabilityBatch_PreservesExceptionalLevelZeroStatesWithoutSourceNotification() {
            UpgradeNodeDefinition inProgressDefinition = CreateDefinition("in_progress");
            UpgradeNodeDefinition completedDefinition = CreateDefinition("completed");
            SetupTree(new[] { inProgressDefinition, completedDefinition }, out UpgradeService upgrades,
                out _, out _);
            UpgradeNodeState inProgress = upgrades.GetUpgrade("in_progress");
            UpgradeNodeState completed = upgrades.GetUpgrade("completed");
            inProgress.CurrentState = UpgradeNodeState.State.InProgress;
            completed.CurrentState = UpgradeNodeState.State.Completed;
            int notifications = 0;
            using IDisposable subscription = upgrades.Changed.Subscribe(_ => notifications++);

            upgrades.ApplyAvailabilityBatch(new Dictionary<string, bool> {
                ["in_progress"] = false,
                ["completed"] = true
            });

            Assert.That(inProgress.CurrentState, Is.EqualTo(UpgradeNodeState.State.InProgress));
            Assert.That(completed.CurrentState, Is.EqualTo(UpgradeNodeState.State.Completed));
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void Restore_RebuildsOwnershipClampsAndUnlocksDescendant() {
            UpgradeNodeDefinition parent = CreateDefinition("parent", maxLevel: 2);
            UpgradeNodeDefinition child = CreateDefinition("child");
            parent.Children = new[] { new UpgradeNodeLink { Child = child, DrawEdge = true } };
            LogAssert.Expect(LogType.Warning,
                "Saved level 9 for upgrade 'parent' exceeds its maximum and was clamped.");

            SetupTree(new[] { parent, child }, out UpgradeService upgrades, out _, out UpgradeTreeService tree);
            upgrades.Deserialize(new JObject {
                ["upgrades"] = new JArray(new JObject { ["id"] = "parent", ["level"] = 9 })
            });

            Assert.That(upgrades.GetUpgrade("parent").Level, Is.EqualTo(2));
            Assert.That(upgrades.GetUpgrade("parent").CurrentState, Is.EqualTo(UpgradeNodeState.State.Completed));
            Assert.That(upgrades.OwnedUpgrades.Count, Is.EqualTo(1));
            Assert.That(tree.IsUnlocked("child"), Is.True);
        }

        [Test]
        public void Restore_MalformedDataIsAtomicAndUnknownIdsAreIgnored() {
            UpgradeNodeDefinition definition = CreateDefinition("known", maxLevel: 2);
            SetupTree(new[] { definition }, out UpgradeService upgrades, out _, out _);
            PurchaseAndComplete(upgrades, "known");
            UpgradeNodeState before = upgrades.GetUpgrade("known");

            Assert.Throws<JsonSerializationException>(() => upgrades.Deserialize(new JObject {
                ["upgrades"] = new JArray(
                    new JObject { ["id"] = "known", ["level"] = 2 },
                    new JObject { ["id"] = "known", ["level"] = 1 })
            }));
            Assert.That(upgrades.GetUpgrade("known"), Is.SameAs(before));

            Assert.Throws<JsonSerializationException>(() => upgrades.Deserialize(new JObject {
                ["upgrades"] = new JArray(new JObject { ["id"] = "known", ["level"] = 0 })
            }));
            Assert.That(upgrades.GetUpgrade("known"), Is.SameAs(before));

            LogAssert.Expect(LogType.Warning,
                "Saved upgrade 'removed_content' is not present in the loaded catalog and was ignored.");
            upgrades.Deserialize(new JObject {
                ["upgrades"] = new JArray(new JObject { ["id"] = "removed_content", ["level"] = 1 })
            });
            Assert.That(upgrades.OwnedUpgrades, Is.Empty);
            Assert.That(upgrades.GetUpgrade("known").Level, Is.Zero);
        }

        [Test]
        public void RuntimeRestore_InvalidatesModifierCachesForAddedAndRemovedOwnership() {
            UpgradeNodeDefinition definition = CreateDefinition("cached_upgrade");
            definition.Modifiers = new[] { CreateGenerationModifier() };
            var upgrades = new UpgradeService(new FakeAssetProvider(new[] { definition }));
            using ServiceScope scope = CreateUpgradeScope(upgrades, out CacheVersionService cache);
            upgrades.InitializeAsync(scope).GetAwaiter().GetResult();
            Assert.That(cache.GetVersion<GenerationEntries>(), Is.Zero);

            upgrades.Deserialize(new JObject {
                ["upgrades"] = new JArray(new JObject { ["id"] = "cached_upgrade", ["level"] = 1 })
            });
            Assert.That(cache.GetVersion<GenerationEntries>(), Is.EqualTo(1));

            upgrades.Deserialize(new JObject { ["upgrades"] = new JArray() });
            Assert.That(cache.GetVersion<GenerationEntries>(), Is.EqualTo(2));
        }

        [Test]
        public void CatalogRejectsEmptyAndDuplicateIds() {
            UpgradeNodeDefinition empty = CreateDefinition("empty");
            empty.Id = " ";
            var service = new UpgradeService(new FakeAssetProvider(new[] { empty }));
            using var scope = CreateUpgradeScope(service);
            Assert.Throws<InvalidOperationException>(() => service.InitializeAsync(scope).GetAwaiter().GetResult());

            UpgradeNodeDefinition first = CreateDefinition("duplicate");
            UpgradeNodeDefinition second = CreateDefinition("duplicate");
            var duplicateService = new UpgradeService(new FakeAssetProvider(new[] { first, second }));
            using var duplicateScope = CreateUpgradeScope(duplicateService);
            Assert.Throws<InvalidOperationException>(() =>
                duplicateService.InitializeAsync(duplicateScope).GetAwaiter().GetResult());
        }

        private readonly List<string> _notificationOrder = new();

        private void SetupTree(IReadOnlyList<UpgradeNodeDefinition> definitions, out UpgradeService upgrades,
            out GameStatisticsService statistics, out UpgradeTreeService tree,
            Func<UpgradeService, List<string>, IDisposable> subscribeBeforeTree = null) {
            var provider = new FakeAssetProvider(definitions);
            upgrades = new UpgradeService(provider);
            statistics = new GameStatisticsService();
            tree = new UpgradeTreeService();
            var scope = CreateUpgradeScope(upgrades);
            _lastWallet = scope.Get<WalletService>();
            scope.Register(statistics).Register(tree);
            _disposables.Add(scope);
            upgrades.InitializeAsync(scope).GetAwaiter().GetResult();

            _notificationOrder.Clear();
            if (subscribeBeforeTree != null) _disposables.Add(subscribeBeforeTree(upgrades, _notificationOrder));
            tree.InitializeAsync(scope).GetAwaiter().GetResult();
        }

        private static ServiceScope CreateUpgradeScope(UpgradeService upgrades) {
            return CreateUpgradeScope(upgrades, out _);
        }

        private static ServiceScope CreateUpgradeScope(UpgradeService upgrades, out CacheVersionService cache) {
            var scope = new ServiceScope(null);
            var wallet = new WalletService();
            wallet.ReplenishWallet(new Value(100));
            cache = new CacheVersionService();
            scope.Register(wallet)
                .Register(cache, typeof(ICacheInvalidator), typeof(ICacheVersionProvider))
                .Register(upgrades);
            return scope;
        }

        private static void PurchaseAndComplete(UpgradeService upgrades, string upgradeId) {
            Assert.That(upgrades.TryUpgrade(upgradeId), Is.True);
            CompletePending(upgrades);
        }

        private static void CompletePending(UpgradeService upgrades, float similarity = 1f, float minimum = 0.4f) {
            Assert.That(upgrades.TryClaimPendingUpgrade(out UpgradeService.UpgradeDocumentClaim claim), Is.True);
            Assert.That(upgrades.TryCompletePendingUpgrade(claim, new SignatureEvaluationResult(
                SignatureEvaluationStatus.Accepted,
                SignatureFailureReason.None,
                similarity,
                minimum,
                null)), Is.True);
        }

        private UpgradeNodeDefinition CreateDefinition(string id, int maxLevel = 1) {
            var definition = ScriptableObject.CreateInstance<UpgradeNodeDefinition>();
            definition.Id = id;
            definition.Name = id;
            definition.MaxLevel = maxLevel;
            definition.CostFormula = new ConstantValue { Value = Value.One };
            definition.Modifiers = Array.Empty<Data.Modifiers.ModifierDefinition>();
            definition.StatisticRequirements = Array.Empty<GameStatisticRequirement>();
            definition.Children = Array.Empty<UpgradeNodeLink>();
            _definitions.Add(definition);
            return definition;
        }

        private static UpgradeNodePresentationModel FindNode(
            IReadOnlyList<UpgradeNodePresentationModel> nodes,
            string id) {
            for (int index = 0; index < nodes.Count; index++) {
                if (nodes[index].Id == id) return nodes[index];
            }

            Assert.Fail($"Node '{id}' was not present in the view model.");
            return null;
        }

        private ModifierDefinition CreateGenerationModifier() {
            var parameter = new CacheParameterReference();
            typeof(CacheParameterReference).GetField("_groupId", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(parameter, "Generation");
            var numeric = new NumericModifierDefinition();
            typeof(NumericModifierDefinition).GetField("_id", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(numeric, "generation");
            typeof(NumericModifierDefinition).GetField("_parameter", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(numeric, parameter);
            var modifier = ScriptableObject.CreateInstance<ModifierDefinition>();
            modifier.NumericModifiers = new List<NumericModifierDefinition> { numeric };
            _modifierDefinitions.Add(modifier);
            return modifier;
        }

        private T Track<T>(T disposable) where T : IDisposable {
            _disposables.Add(disposable);
            return disposable;
        }

        private sealed class FakeAssetProvider : IAssetProvider {
            private readonly IReadOnlyList<UpgradeNodeDefinition> _definitions;

            public FakeAssetProvider(IReadOnlyList<UpgradeNodeDefinition> definitions) {
                _definitions = definitions;
            }

            public UniTask<IAssetLease<T>> LoadAsync<T>(AssetReferenceT<T> reference) where T : UnityEngine.Object {
                throw new NotSupportedException();
            }

            public UniTask<IAssetListLease<T>> LoadAssetsByLabelAsync<T>(string label) where T : UnityEngine.Object {
                if (typeof(T) != typeof(UpgradeNodeDefinition)) throw new NotSupportedException();
                return UniTask.FromResult<IAssetListLease<T>>(new FakeAssetListLease<T>(_definitions));
            }

            public UniTask<IInstanceLease> InstantiateAsync(AssetReference instanceReference, Transform parent = null,
                bool worldPositionStays = false) {
                throw new NotSupportedException();
            }
        }

        private sealed class FakeAssetListLease<T> : IAssetListLease<T> where T : UnityEngine.Object {
            private readonly IReadOnlyList<T> _assets;

            public IReadOnlyList<T> Assets => _assets;

            public FakeAssetListLease(IReadOnlyList<UpgradeNodeDefinition> definitions) {
                var assets = new List<T>(definitions.Count);
                for (int index = 0; index < definitions.Count; index++) assets.Add((T)(object)definitions[index]);
                _assets = assets;
            }

            public void Dispose() { }
        }
    }
}
