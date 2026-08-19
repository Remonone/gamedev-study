using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Authoring;
using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Enums;
using Data.Rules;
using Data.Templates;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using R3;
using Services;
using Services.Locator;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode {
    public sealed class SignatureProgressionServiceTests {
        private readonly List<UnityEngine.Object> _objects = new();
        private readonly List<ServiceScope> _scopes = new();

        [TearDown]
        public void TearDown() {
            for (int index = _scopes.Count - 1; index >= 0; index--) _scopes[index].Dispose();
            _scopes.Clear();
            for (int index = _objects.Count - 1; index >= 0; index--) {
                if (_objects[index] != null) UnityEngine.Object.DestroyImmediate(_objects[index]);
            }
            _objects.Clear();
        }

        [Test]
        public void NewGame_GeneratesFourCategoryOffers_AndUnlocksOnlyChoice() {
            FakeRepository repository = CreateRepository(4, 1);
            SignatureProgressionService progression = Initialize(
                new SignatureProgressionService(GameLaunchMode.NewGame, upperBound => upperBound - 1), repository);

            Assert.That(progression.PendingOfferIds, Has.Count.EqualTo(4));
            Assert.That(new HashSet<string>(progression.PendingOfferIds), Has.Count.EqualTo(4));
            string selected = progression.PendingOfferIds[1];
            int notifications = 0;
            using IDisposable subscription = progression.ActivePresetChanged.Subscribe(_ => notifications++);

            Assert.That(progression.TrySelectStartingPreset(selected), Is.True);
            Assert.That(progression.ActivePresetId, Is.EqualTo(selected));
            Assert.That(progression.UnlockedPresetIds, Is.EqualTo(new[] { selected }));
            Assert.That(progression.PendingOfferIds, Is.Empty);
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(progression.TrySelectStartingPreset(selected), Is.False);
        }

        [Test]
        public void NewGame_WithMissingSignatureCategory_FailsClearly() {
            FakeRepository repository = CreateRepository(2, 2);
            var progression = new SignatureProgressionService(GameLaunchMode.NewGame, _ => 0);
            using var scope = new ServiceScope(null);
            scope.Register<ISignaturePresetRepository>(repository).Register(progression);

            Assert.Throws<InvalidOperationException>(() =>
                progression.InitializeAsync(scope).GetAwaiter().GetResult());
        }

        [Test]
        public void PendingOffers_RoundTripIntoContinueWithoutReroll() {
            FakeRepository repository = CreateRepository(4, 0);
            SignatureProgressionService first = Initialize(
                new SignatureProgressionService(GameLaunchMode.NewGame, _ => 0), repository);
            string[] offered = new List<string>(first.PendingOfferIds).ToArray();
            JToken state = first.Serialize();

            var restored = new SignatureProgressionService(GameLaunchMode.Continue,
                _ => throw new AssertionException("Valid pending offers must not reroll."));
            restored.Deserialize(state);
            Initialize(restored, repository);

            Assert.That(restored.PendingOfferIds, Is.EqualTo(offered));
            Assert.That(restored.RequiresStartingSelection, Is.True);
        }

        [Test]
        public void RestoredState_NormalizesDuplicatesAndActiveUnlockInvariant() {
            FakeRepository repository = CreateRepository(4, 0);
            string active = repository.Presets[1].Id;
            var progression = new SignatureProgressionService(GameLaunchMode.Continue, _ => 0);
            progression.Deserialize(new JObject {
                ["activePresetId"] = active,
                ["unlockedPresetIds"] = new JArray(repository.Presets[0].Id, repository.Presets[0].Id, "missing"),
                ["pendingOfferIds"] = new JArray()
            });

            Initialize(progression, repository);

            Assert.That(progression.ActivePresetId, Is.EqualTo(active));
            Assert.That(progression.UnlockedPresetIds, Is.EquivalentTo(new[] { repository.Presets[0].Id, active }));
            Assert.That(progression.PendingOfferIds, Is.Empty);
        }

        [Test]
        public void ContinueWithoutProgressionSection_UsesFormerDefault() {
            var presets = new List<SignaturePresetDefinition> {
                CreatePreset("starter", true),
                CreatePreset("test_preset", false),
                CreatePreset("other", true),
                CreatePreset("third", true)
            };
            var repository = new FakeRepository(presets);

            SignatureProgressionService progression = Initialize(
                new SignatureProgressionService(GameLaunchMode.Continue, _ => 0), repository);

            Assert.That(progression.ActivePresetId, Is.EqualTo("test_preset"));
            Assert.That(progression.UnlockedPresetIds, Is.EqualTo(new[] { "test_preset" }));
            Assert.That(progression.RequiresStartingSelection, Is.False);
        }

        [Test]
        public void SelectingActivePreset_ClearsLoaderCacheAndInvalidatesSignatureAndIncomeEntries() {
            FakeRepository repository = CreateRepository(4, 0);
            Assert.That(repository.Presets[3].Category, Is.EqualTo(SignatureCategory.Elegant));
            Assert.That(repository.Presets[3].HasTag(InternalConstants.STARTING_SIGNATURE_TAG), Is.True);
            var progression = new SignatureProgressionService(GameLaunchMode.NewGame, _ => 0);
            var cache = new CacheVersionService();
            var loader = new SelectedSignatureLoader();
            var scope = new ServiceScope(null);
            _scopes.Add(scope);
            scope.Register<ISignaturePresetRepository>(repository)
                .Register(progression)
                .Register(cache, typeof(ICacheVersionProvider), typeof(ICacheInvalidator))
                .Register(loader);
            progression.InitializeAsync(scope).GetAwaiter().GetResult();
            loader.InitializeAsync(scope).GetAwaiter().GetResult();
            FieldInfo cachedRules = typeof(SelectedSignatureLoader).GetField(
                "_baseDifficulty", BindingFlags.Instance | BindingFlags.NonPublic);
            cachedRules.SetValue(loader, new SignatureDifficultyRules(
                "test", 0.5f, 1f, 1f, 1f, new SignatureScoreWeights(1f, 1f, 1f, 1f)));

            Assert.That(progression.TrySelectStartingPreset(progression.PendingOfferIds[0]), Is.True);

            Assert.That(cachedRules.GetValue(loader), Is.Null);
            Assert.That(cache.GetVersion<SignatureEntries>(), Is.EqualTo(1));
            Assert.That(cache.GetVersion<IncomeEntries>(), Is.EqualTo(1));
        }

        [Test]
        public void NoActivePreset_ExposesSafeBaselineStateBeforeSelection() {
            FakeRepository repository = CreateRepository(4, 0);
            var progression = new SignatureProgressionService(GameLaunchMode.NewGame, _ => 0);
            var cache = new CacheVersionService();
            var loader = new SelectedSignatureLoader();
            var scope = new ServiceScope(null);
            _scopes.Add(scope);
            scope.Register<ISignaturePresetRepository>(repository)
                .Register(progression)
                .Register(cache, typeof(ICacheVersionProvider), typeof(ICacheInvalidator))
                .Register(loader);
            progression.InitializeAsync(scope).GetAwaiter().GetResult();
            loader.InitializeAsync(scope).GetAwaiter().GetResult();

            Assert.That(loader.TryGetActivePreset(out _), Is.False);
            Assert.That(loader.TryGetBaseIncome(out _), Is.False);
        }

        [Test]
        public void ProductionPresets_HaveFourUniqueCategoriesAndPositiveBaseIncome() {
            string[] paths = {
                "Assets/_SigningGame/Data/SignaturePreset.asset",
                "Assets/_SigningGame/Data/Starter Signature Three.asset",
                "Assets/_SigningGame/Data/Test Preset.asset",
                "Assets/_SigningGame/Data/Elegant Signature.asset"
            };
            var categories = new HashSet<SignatureCategory>();
            for (int index = 0; index < paths.Length; index++) {
                SignaturePresetDefinition preset = AssetDatabase.LoadAssetAtPath<SignaturePresetDefinition>(paths[index]);
                Assert.That(preset, Is.Not.Null, paths[index]);
                Assert.That(preset.BaseIncome.IsZero, Is.False, paths[index]);
                Assert.That(categories.Add(preset.Category), Is.True, paths[index]);
            }

            Assert.That(categories, Is.EquivalentTo(new[] {
                SignatureCategory.Simple, SignatureCategory.Medium,
                SignatureCategory.Complex, SignatureCategory.Elegant
            }));
            string addressables = File.ReadAllText("Assets/AddressableAssetsData/AssetGroups/SigningGame.asset");
            Assert.That(addressables, Does.Contain("Assets/_SigningGame/Data/Elegant Signature.asset"));
            Assert.That(addressables, Does.Contain("d7f7f3c8c1f842a4b8d7d1f2f4de9a60"));
        }

        private SignatureProgressionService Initialize(
            SignatureProgressionService progression,
            FakeRepository repository) {
            var scope = new ServiceScope(null);
            _scopes.Add(scope);
            scope.Register<ISignaturePresetRepository>(repository).Register(progression);
            progression.InitializeAsync(scope).GetAwaiter().GetResult();
            return progression;
        }

        private FakeRepository CreateRepository(int starterCount, int lockedCount) {
            var presets = new List<SignaturePresetDefinition>();
            for (int index = 0; index < starterCount; index++) {
                SignatureCategory category = (SignatureCategory)(index % 4);
                presets.Add(CreatePreset($"starter_{index}", true, category));
            }
            for (int index = 0; index < lockedCount; index++) presets.Add(CreatePreset($"locked_{index}", false));
            return new FakeRepository(presets);
        }

        private SignaturePresetDefinition CreatePreset(string id, bool starter,
            SignatureCategory category = SignatureCategory.Simple) {
            SignaturePresetDefinition preset = ScriptableObject.CreateInstance<SignaturePresetDefinition>();
            _objects.Add(preset);
            var serialized = new SerializedObject(preset);
            serialized.FindProperty("_id").stringValue = id;
            serialized.FindProperty("_displayName").stringValue = id;
            serialized.FindProperty("_category").enumValueIndex = (int)category;
            serialized.FindProperty("_baseIncome._stored").doubleValue = 1d + (int)category;
            SerializedProperty tags = serialized.FindProperty("_tags");
            tags.arraySize = starter ? 1 : 0;
            if (starter) tags.GetArrayElementAtIndex(0).stringValue = InternalConstants.STARTING_SIGNATURE_TAG;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return preset;
        }

        private sealed class FakeRepository : ISignaturePresetRepository, IService {
            private readonly Dictionary<string, SignaturePresetDefinition> _byId = new(StringComparer.Ordinal);
            public IReadOnlyList<SignaturePresetDefinition> Presets { get; }

            public FakeRepository(IReadOnlyList<SignaturePresetDefinition> presets) {
                Presets = presets;
                for (int index = 0; index < presets.Count; index++) _byId.Add(presets[index].Id, presets[index]);
            }

            public bool TryGetPreset(string id, out SignaturePresetDefinition preset) => _byId.TryGetValue(id, out preset);
            public UniTask<SignaturePresetDefinition> RequestPreset(string id) => UniTask.FromResult(_byId[id]);
            public CompiledSignaturePreset GetOrCompile(SignaturePresetDefinition preset) => throw new NotSupportedException();
            public void Invalidate(SignaturePresetDefinition preset) { }
            public void InvalidateById(string presetId) { }
            public void Clear() { }
            public void Dispose() { }
        }
    }
}
