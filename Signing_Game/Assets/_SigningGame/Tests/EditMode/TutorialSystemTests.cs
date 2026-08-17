using System;
using System.Collections.Generic;
using System.Reflection;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Tutorial;
using Data.Upgrades;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Services;
using Services.Locator;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Tests.EditMode {
    public sealed class TutorialSystemTests {
        private readonly List<UnityEngine.Object> _objects = new();
        private readonly List<IDisposable> _disposables = new();

        [TearDown]
        public void TearDown() {
            for (int index = _disposables.Count - 1; index >= 0; index--) _disposables[index].Dispose();
            _disposables.Clear();
            for (int index = 0; index < _objects.Count; index++) UnityEngine.Object.DestroyImmediate(_objects[index]);
            _objects.Clear();
        }

        // --- Trigger evaluation ---

        [Test]
        public void StatisticsTrigger_ComparisonsAreEvaluated() {
            var statistics = new GameStatisticsService();
            var upgrades = new UpgradeService(new FakeAssetProvider());
            var context = new TutorialTriggerContext(statistics, upgrades);
            _disposables.Add(statistics);
            _disposables.Add(upgrades);
            statistics.SetValue("stat", 5d);

            Assert.That(IsSatisfied(Trigger("stat", TutorialStatisticComparison.GreaterOrEqual, 5d), context), Is.True);
            Assert.That(IsSatisfied(Trigger("stat", TutorialStatisticComparison.GreaterOrEqual, 6d), context), Is.False);
            Assert.That(IsSatisfied(Trigger("stat", TutorialStatisticComparison.Greater, 4d), context), Is.True);
            Assert.That(IsSatisfied(Trigger("stat", TutorialStatisticComparison.Greater, 5d), context), Is.False);
            Assert.That(IsSatisfied(Trigger("stat", TutorialStatisticComparison.Equal, 5d), context), Is.True);
            Assert.That(IsSatisfied(Trigger("stat", TutorialStatisticComparison.Equal, 4d), context), Is.False);
            Assert.That(IsSatisfied(Trigger("stat", TutorialStatisticComparison.NotEqual, 4d), context), Is.True);
            Assert.That(IsSatisfied(Trigger("stat", TutorialStatisticComparison.NotEqual, 5d), context), Is.False);
            Assert.That(IsSatisfied(Trigger("stat", TutorialStatisticComparison.Less, 6d), context), Is.True);
            Assert.That(IsSatisfied(Trigger("stat", TutorialStatisticComparison.Less, 5d), context), Is.False);
            Assert.That(IsSatisfied(Trigger("stat", TutorialStatisticComparison.LessOrEqual, 5d), context), Is.True);
            Assert.That(IsSatisfied(Trigger("stat", TutorialStatisticComparison.LessOrEqual, 4d), context), Is.False);
        }

        [Test]
        public void StatisticsTrigger_MissingRowNeverSatisfies() {
            var statistics = new GameStatisticsService();
            var upgrades = new UpgradeService(new FakeAssetProvider());
            var context = new TutorialTriggerContext(statistics, upgrades);
            _disposables.Add(statistics);
            _disposables.Add(upgrades);

            foreach (TutorialStatisticComparison comparison in Enum.GetValues(typeof(TutorialStatisticComparison))) {
                Assert.That(IsSatisfied(Trigger("missing", comparison, 0d), context), Is.False,
                    $"Comparison {comparison} must fail for a missing statistic row.");
            }

            Assert.That(IsSatisfied(Trigger("  ", TutorialStatisticComparison.Equal, 0d), context), Is.False);
        }

        [Test]
        public void UpgradeTrigger_RequiresLevelAndKnownUpgrade() {
            Harness harness = CreateHarness(Array.Empty<TutorialDefinition>(), true,
                CreateUpgradeDefinition("upg"));
            var context = new TutorialTriggerContext(harness.Statistics, harness.Upgrades);

            var trigger = new UpgradeTrigger { UpgradeId = "upg", MinLevel = 1 };
            Assert.That(trigger.IsSatisfied(context), Is.False, "Not purchased yet.");

            CompleteUpgrade(harness.Upgrades, harness.Wallet, "upg");
            Assert.That(trigger.IsSatisfied(context), Is.True);

            Assert.That(new UpgradeTrigger { UpgradeId = "upg", MinLevel = 2 }.IsSatisfied(context), Is.False);
            Assert.That(new UpgradeTrigger { UpgradeId = "unknown", MinLevel = 1 }.IsSatisfied(context), Is.False);
            Assert.That(new UpgradeTrigger { UpgradeId = "", MinLevel = 1 }.IsSatisfied(context), Is.False);
        }

        // --- Restore protection ---

        [Test]
        public void TriggerSatisfiedBeforeInitialization_NeverActivates() {
            TutorialDefinition definition = CreateTutorial(
                "t1",
                Trigger("stat", TutorialStatisticComparison.GreaterOrEqual, 10d),
                Slide("Hello", new ClickCondition()));
            Harness harness = CreateHarness(new[] { definition });

            harness.Statistics.SetValue("stat", 25d); // restored / pre-existing state
            harness.InitializeTutorial();

            Assert.That(harness.Tutorial.HasActive, Is.False);

            harness.Statistics.SetValue("stat", 30d); // still satisfied, no false→true edge
            Assert.That(harness.Tutorial.HasActive, Is.False);
        }

        [Test]
        public void DeferredUpgradeRestore_DoesNotActivateTrigger() {
            TutorialDefinition definition = CreateTutorial(
                "t1",
                new UpgradeTrigger { UpgradeId = "upg", MinLevel = 1 },
                Slide("Hello", new ClickCondition()));
            Harness harness = CreateHarness(new[] { definition }, false, CreateUpgradeDefinition("upg"));

            // Deferred restore path: Deserialize before the upgrade catalog is built.
            harness.Upgrades.Deserialize(new JObject {
                ["upgrades"] = new JArray(new JObject {
                    ["id"] = "upg",
                    ["level"] = 1,
                    ["effectiveness"] = 1f
                })
            });
            harness.Upgrades.InitializeAsync(harness.Scope).GetAwaiter().GetResult();
            harness.InitializeTutorial();

            Assert.That(harness.Tutorial.HasActive, Is.False);
        }

        [Test]
        public void LiveTransition_ActivatesPopup() {
            TutorialDefinition definition = CreateTutorial(
                "t1",
                Trigger("stat", TutorialStatisticComparison.GreaterOrEqual, 10d),
                Slide("First", new ClickCondition()),
                Slide("Second", new ClickCondition()));
            Harness harness = CreateHarness(new[] { definition });
            harness.InitializeTutorial();

            harness.Statistics.SetValue("stat", 11d);

            Assert.That(harness.Tutorial.HasActive, Is.True);
            Assert.That(harness.Tutorial.ActiveDefinition, Is.SameAs(definition));
            Assert.That(harness.Tutorial.SlideIndex, Is.Zero);
        }

        // --- Slide advancement ---

        [Test]
        public void Click_BeforeTypingCompleted_DoesNotAdvance() {
            TutorialDefinition definition = CreateTutorial(
                "t1",
                Trigger("stat", TutorialStatisticComparison.Greater, 0d),
                Slide("First", new ClickCondition()),
                Slide("Second", new ClickCondition()));
            Harness harness = CreateHarness(new[] { definition });
            harness.InitializeTutorial();
            harness.Statistics.SetValue("stat", 1d);

            harness.Tutorial.NotifyInteraction(new TutorialInteractionEvent(TutorialInteractionKind.PlayerClick));
            Assert.That(harness.Tutorial.SlideIndex, Is.Zero, "Clicks during typing must not advance.");

            harness.Tutorial.NotifyTypingCompleted();
            harness.Tutorial.NotifyInteraction(new TutorialInteractionEvent(TutorialInteractionKind.PlayerClick));
            Assert.That(harness.Tutorial.SlideIndex, Is.EqualTo(1));
        }

        [Test]
        public void OpenTabCondition_AdvancesOnlyOnMatchingTab() {
            TutorialDefinition definition = CreateTutorial(
                "t1",
                Trigger("stat", TutorialStatisticComparison.Greater, 0d),
                Slide("Open the office tab", new OpenTabCondition { TabId = "office" }));
            Harness harness = CreateHarness(new[] { definition });
            harness.InitializeTutorial();
            harness.Statistics.SetValue("stat", 1d);
            harness.Tutorial.NotifyTypingCompleted();

            harness.Tutorial.NotifyInteraction(new TutorialInteractionEvent(TutorialInteractionKind.TabOpened, "bank"));
            Assert.That(harness.Tutorial.HasActive, Is.True);

            harness.Tutorial.NotifyInteraction(new TutorialInteractionEvent(TutorialInteractionKind.PlayerClick));
            Assert.That(harness.Tutorial.HasActive, Is.True);

            harness.Tutorial.NotifyInteraction(new TutorialInteractionEvent(TutorialInteractionKind.TabOpened, "office"));
            Assert.That(harness.Tutorial.HasActive, Is.False, "Last slide completion closes the popup.");
        }

        [Test]
        public void PendingQueue_ActivatesNextPopupAfterCompletion() {
            TutorialDefinition first = CreateTutorial(
                "first",
                Trigger("stat", TutorialStatisticComparison.Greater, 0d),
                Slide("One", new ClickCondition()));
            TutorialDefinition second = CreateTutorial(
                "second",
                Trigger("other", TutorialStatisticComparison.GreaterOrEqual, 1d),
                Slide("Two", new ClickCondition()));
            Harness harness = CreateHarness(new[] { first, second });
            harness.InitializeTutorial();

            harness.Statistics.SetValue("stat", 1d);
            harness.Statistics.SetValue("other", 1d);
            Assert.That(harness.Tutorial.ActiveDefinition, Is.SameAs(first),
                "First registered definition activates first.");

            harness.Tutorial.NotifyTypingCompleted();
            harness.Tutorial.NotifyInteraction(new TutorialInteractionEvent(TutorialInteractionKind.PlayerClick));
            Assert.That(harness.Tutorial.ActiveDefinition, Is.SameAs(second),
                "Queued definition activates after the active popup completes.");
        }

        // --- Persistence ---

        [Test]
        public void CompletedDefinitions_ArePersistedAndNotReactivated() {
            TutorialDefinition definition = CreateTutorial(
                "t1",
                Trigger("stat", TutorialStatisticComparison.Greater, 0d),
                Slide("Hello", new ClickCondition()));
            Harness harness = CreateHarness(new[] { definition });
            harness.InitializeTutorial();

            harness.Statistics.SetValue("stat", 1d);
            harness.Tutorial.NotifyTypingCompleted();
            harness.Tutorial.NotifyInteraction(new TutorialInteractionEvent(TutorialInteractionKind.PlayerClick));
            Assert.That(harness.Tutorial.HasActive, Is.False);

            JToken saved = harness.Tutorial.Serialize();
            harness.Statistics.SetValue("stat", 0d);
            harness.Statistics.SetValue("stat", 1d);
            Assert.That(harness.Tutorial.HasActive, Is.False, "Completed definitions never reactivate.");

            TutorialDefinition restoredDefinition = CreateTutorial(
                "t1",
                Trigger("stat", TutorialStatisticComparison.Greater, 0d),
                Slide("Hello", new ClickCondition()));
            Harness restored = CreateHarness(new[] { restoredDefinition });
            restored.Tutorial.Deserialize(saved); // PreInitialize-phase restore
            restored.InitializeTutorial();

            restored.Statistics.SetValue("stat", 2d);
            Assert.That(restored.Tutorial.HasActive, Is.False,
                "A restored save must suppress already-completed popups.");
        }

        [Test]
        public void Deserialize_RejectsMalformedState() {
            var tutorial = new TutorialService();
            _disposables.Add(tutorial);

            Assert.Throws<Newtonsoft.Json.JsonSerializationException>(() => tutorial.Deserialize(new JArray()));
            Assert.Throws<Newtonsoft.Json.JsonSerializationException>(
                () => tutorial.Deserialize(new JObject { ["completed"] = new JArray(1, 2) }));
            Assert.Throws<Newtonsoft.Json.JsonSerializationException>(
                () => tutorial.Deserialize(new JObject { ["completed"] = new JArray("") }));
        }

        [Test]
        public void InvalidDefinitions_AreExcluded() {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                "Tutorial definition 'broken' was excluded: no trigger is assigned.");
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                "Tutorial definition 'empty' was excluded: there are no slides.");
            TutorialDefinition noTrigger = CreateTutorial("broken", null, Slide("text", new ClickCondition()));
            TutorialDefinition noSlides = CreateTutorial(
                "empty",
                Trigger("stat", TutorialStatisticComparison.Equal, 0d));

            Harness harness = CreateHarness(new TutorialDefinition[] { noTrigger, noSlides });
            harness.InitializeTutorial();

            harness.Statistics.SetValue("stat", 0d);
            Assert.That(harness.Tutorial.HasActive, Is.False);
        }

        // --- Helpers ---

        private static bool IsSatisfied(TutorialTriggerDefinition trigger, TutorialTriggerContext context) {
            return trigger.IsSatisfied(context);
        }

        private static StatisticsTrigger Trigger(string statisticId, TutorialStatisticComparison comparison,
            double target) {
            return new StatisticsTrigger {
                StatisticId = statisticId,
                Comparison = comparison,
                TargetValue = target
            };
        }

        private static TutorialSlide Slide(string text, TutorialSlideCondition condition) {
            var slide = new TutorialSlide();
            SetField(slide, "_text", text);
            SetField(slide, "_advanceCondition", condition);
            return slide;
        }

        private TutorialDefinition CreateTutorial(string id, TutorialTriggerDefinition trigger,
            params TutorialSlide[] slides) {
            var definition = ScriptableObject.CreateInstance<TutorialDefinition>();
            definition.name = id;
            _objects.Add(definition);
            SetField(definition, "_id", id);
            SetField(definition, "_trigger", trigger);
            SetField(definition, "_slides", slides);
            return definition;
        }

        private UpgradeNodeDefinition CreateUpgradeDefinition(string id) {
            var definition = ScriptableObject.CreateInstance<UpgradeNodeDefinition>();
            _objects.Add(definition);
            definition.Id = id;
            definition.Name = id;
            definition.MaxLevel = 1;
            definition.CostFormula = new Data.Formulas.ConstantValue { Value = Utils.Value.One };
            definition.Modifiers = Array.Empty<Data.Modifiers.ModifierDefinition>();
            definition.FeatureUnlockIds = Array.Empty<string>();
            definition.StatisticRequirements = Array.Empty<GameStatisticRequirement>();
            definition.Children = Array.Empty<UpgradeNodeLink>();
            return definition;
        }

        private static void SetField(object target, string fieldName, object value) {
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Field {fieldName} not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private Harness CreateHarness(IReadOnlyList<TutorialDefinition> tutorials,
            bool initializeUpgrades = true,
            params UpgradeNodeDefinition[] upgradeDefinitions) {
            var scope = new ServiceScope(null);
            var statistics = new GameStatisticsService();
            var wallet = new WalletService();
            wallet.ReplenishWallet(new Utils.Value(100d));
            var cache = new CacheVersionService();
            var upgrades = new UpgradeService(new FakeAssetProvider(upgradeDefinitions));
            var tutorial = new TutorialService();
            tutorial.SetDefinitions(tutorials);

            scope.Register(statistics)
                .Register(wallet)
                .Register(cache, typeof(Data.Cache.ICacheInvalidator), typeof(Data.Cache.ICacheVersionProvider))
                .Register(upgrades)
                .Register(tutorial);

            if (initializeUpgrades) {
                upgrades.InitializeAsync(scope).GetAwaiter().GetResult();
            }

            _disposables.Add(tutorial);
            _disposables.Add(upgrades);
            _disposables.Add(statistics);
            return new Harness(scope, statistics, upgrades, wallet, tutorial);
        }

        private static void CompleteUpgrade(UpgradeService upgrades, WalletService wallet, string id) {
            SetAllAvailable(upgrades);
            Assert.That(upgrades.TryUpgrade(id), Is.True);
            Assert.That(upgrades.TryClaimPendingUpgrade(out UpgradeService.UpgradeDocumentClaim claim), Is.True);
            Assert.That(upgrades.TryCompletePendingUpgrade(claim, new Data.Results.SignatureEvaluationResult(
                Data.Enums.SignatureEvaluationStatus.Accepted,
                Data.Enums.SignatureFailureReason.None,
                1f,
                0.4f,
                null)), Is.True);
        }

        private static void SetAllAvailable(UpgradeService upgrades) {
            var availability = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (UpgradeNodeState state in upgrades.Nodes) availability.Add(state.Definition.Id, true);
            upgrades.ApplyAvailabilityBatch(availability);
        }

        private sealed class Harness {
            public Harness(ServiceScope scope, GameStatisticsService statistics, UpgradeService upgrades,
                WalletService wallet, TutorialService tutorial) {
                Scope = scope;
                Statistics = statistics;
                Upgrades = upgrades;
                Wallet = wallet;
                Tutorial = tutorial;
            }

            public ServiceScope Scope { get; }
            public GameStatisticsService Statistics { get; }
            public UpgradeService Upgrades { get; }
            public WalletService Wallet { get; }
            public TutorialService Tutorial { get; }

            public void InitializeTutorial() {
                Tutorial.InitializeAsync(Scope).GetAwaiter().GetResult();
            }
        }

        private sealed class FakeAssetProvider : IAssetProvider, IService {
            private readonly IReadOnlyList<UpgradeNodeDefinition> _upgrades;

            public FakeAssetProvider(params UpgradeNodeDefinition[] upgrades) => _upgrades = upgrades;

            public UniTask<IAssetLease<T>> LoadAsync<T>(AssetReferenceT<T> reference) where T : UnityEngine.Object {
                throw new NotSupportedException();
            }

            public UniTask<IAssetListLease<T>> LoadAssetsByLabelAsync<T>(string label)
                where T : UnityEngine.Object {
                if (typeof(T) == typeof(UpgradeNodeDefinition)) {
                    var assets = new T[_upgrades.Count];
                    for (int index = 0; index < assets.Length; index++) assets[index] = (T)(object)_upgrades[index];
                    return UniTask.FromResult<IAssetListLease<T>>(new FakeAssetListLease<T>(assets));
                }

                return UniTask.FromResult<IAssetListLease<T>>(new FakeAssetListLease<T>(Array.Empty<T>()));
            }

            public UniTask<IInstanceLease> InstantiateAsync(AssetReference instanceReference, Transform parent = null,
                bool worldPositionStays = false) {
                throw new NotSupportedException();
            }

            public void Dispose() { }
        }

        private sealed class FakeAssetListLease<T> : IAssetListLease<T> where T : UnityEngine.Object {
            public IReadOnlyList<T> Assets { get; }

            public FakeAssetListLease(IReadOnlyList<T> assets) => Assets = assets;

            public void Dispose() { }
        }
    }
}
