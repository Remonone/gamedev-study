using System;
using System.Collections.Generic;
using System.IO;
using Contracts;
using Data.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Presentation;
using R3;
using Services;
using Services.Locator;
using UnityEngine;

namespace Tests.EditMode {
    public class SaveServiceTests {
        private string _directory;
        private string _filePath;

        [SetUp]
        public void SetUp() {
            _directory = Path.Combine(Path.GetTempPath(), $"SigningGame_SaveTests_{Guid.NewGuid():N}");
            _filePath = Path.Combine(_directory, "save.json");
        }

        [TearDown]
        public void TearDown() {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        [Test]
        public void Snapshot_RoundTripsSectionedState() {
            using var scope = new ServiceScope(null);
            var saveService = new SaveService(_filePath);
            var first = new FakeSaveable("first", 10);
            var second = new FakeSaveable("second", 20);
            scope.Register(saveService).Register(first).Register(second);
            saveService.PreInitializeAsync(scope).GetAwaiter().GetResult();

            SaveSnapshot snapshot = saveService.CreateSnapshot();
            first.State = 0;
            second.State = 0;

            Assert.That(saveService.LoadSnapshot(snapshot), Is.True);
            Assert.That(first.State, Is.EqualTo(10));
            Assert.That(second.State, Is.EqualTo(20));
            Assert.That(snapshot.Sections.Keys, Is.EquivalentTo(new[] { "first", "second" }));
        }

        [Test]
        public void PreInitialize_RestoresServicesRegisteredAfterSaveService() {
            using (var writeScope = new ServiceScope(null)) {
                var writer = new SaveService(_filePath);
                var source = new FakeSaveable("state", 42);
                writeScope.Register(writer).Register(source);
                writer.PreInitializeAsync(writeScope).GetAwaiter().GetResult();
                Assert.That(writer.SaveToFile(), Is.True);
                source.State = 43;
                Assert.That(writer.SaveToFile(), Is.True);
            }

            using var readScope = new ServiceScope(null);
            var reader = new SaveService(_filePath);
            var restored = new FakeSaveable("state", 0);
            readScope.Register(reader).Register(restored);

            reader.PreInitializeAsync(readScope).GetAwaiter().GetResult();

            Assert.That(restored.State, Is.EqualTo(43));
        }

        [Test]
        public void LoadFromFile_MissingCorruptOrUnsupportedSchema_DoesNotMutateState() {
            using var scope = new ServiceScope(null);
            var saveService = new SaveService(_filePath);
            var saveable = new FakeSaveable("state", 7);
            scope.Register(saveService).Register(saveable);
            saveService.PreInitializeAsync(scope).GetAwaiter().GetResult();

            Assert.That(saveService.TryLoadFromFile(), Is.False);
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_filePath, "not json");
            Assert.That(saveService.TryLoadFromFile(), Is.False);
            Assert.That(saveable.State, Is.EqualTo(7));

            File.WriteAllText(_filePath, "{\"sections\":{\"state\":99}}");
            Assert.That(saveService.TryLoadFromFile(), Is.False);
            Assert.That(saveable.State, Is.EqualTo(7));

            File.WriteAllText(_filePath, "{\"version\":2,\"sections\":{\"state\":99}}");
            Assert.That(saveService.TryLoadFromFile(), Is.False);
            Assert.That(saveable.State, Is.EqualTo(7));
        }

        [Test]
        public void HasValidSave_RequiresSupportedSectionedSnapshot() {
            Assert.That(SaveService.HasValidSave(_filePath), Is.False);
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_filePath, "not json");
            Assert.That(SaveService.HasValidSave(_filePath), Is.False);
            File.WriteAllText(_filePath, "{\"version\":2,\"sections\":{}}");
            Assert.That(SaveService.HasValidSave(_filePath), Is.False);
            File.WriteAllText(_filePath, JsonConvert.SerializeObject(new SaveSnapshot()));
            Assert.That(SaveService.HasValidSave(_filePath), Is.True);
        }

        [Test]
        public void PreInitialize_WhenLoadDisabled_DoesNotRestoreExistingState() {
            using (var writeScope = new ServiceScope(null)) {
                var writer = new SaveService(_filePath);
                var source = new FakeSaveable("state", 42);
                writeScope.Register(writer).Register(source);
                writer.PreInitializeAsync(writeScope).GetAwaiter().GetResult();
                Assert.That(writer.SaveToFile(), Is.True);
            }

            using var readScope = new ServiceScope(null);
            var reader = new SaveService(_filePath, loadExistingOnInitialize: false);
            var state = new FakeSaveable("state", 7);
            readScope.Register(reader).Register(state);
            reader.PreInitializeAsync(readScope).GetAwaiter().GetResult();

            Assert.That(state.State, Is.EqualTo(7));
        }

        [Test]
        public void LoadSnapshot_MalformedSectionDoesNotBlockValidSection() {
            using var scope = new ServiceScope(null);
            var saveService = new SaveService(_filePath);
            var valid = new FakeSaveable("valid", 1);
            var malformed = new FakeSaveable("malformed", 2);
            scope.Register(saveService).Register(valid).Register(malformed);
            saveService.PreInitializeAsync(scope).GetAwaiter().GetResult();
            var snapshot = new SaveSnapshot(SaveSnapshot.CurrentVersion, new Dictionary<string, JToken> {
                ["valid"] = new JValue(11),
                ["malformed"] = new JObject()
            });

            Assert.That(saveService.LoadSnapshot(snapshot), Is.False);
            Assert.That(valid.State, Is.EqualTo(11));
            Assert.That(malformed.State, Is.EqualTo(2));
        }

        [Test]
        public void SaveToFile_SerializationFailureKeepsExistingSave() {
            using var scope = new ServiceScope(null);
            var saveService = new SaveService(_filePath);
            var saveable = new FakeSaveable("state", 5);
            scope.Register(saveService).Register(saveable);
            saveService.PreInitializeAsync(scope).GetAwaiter().GetResult();
            Assert.That(saveService.SaveToFile(), Is.True);
            string originalJson = File.ReadAllText(_filePath);

            saveable.ThrowOnSerialize = true;

            Assert.That(saveService.SaveToFile(), Is.False);
            Assert.That(File.ReadAllText(_filePath), Is.EqualTo(originalJson));
            Assert.That(File.Exists(_filePath + ".tmp"), Is.False);
        }

        [Test]
        public void PreInitialize_RejectsDuplicateSaveIds() {
            using var scope = new ServiceScope(null);
            var saveService = new SaveService(_filePath);
            scope.Register(saveService)
                .Register(new FakeSaveable("duplicate", 1))
                .Register(new FakeSaveable("duplicate", 2));

            Assert.Throws<InvalidOperationException>(() =>
                saveService.PreInitializeAsync(scope).GetAwaiter().GetResult());
        }

        [Test]
        public void DocumentViewModel_ReplaysRestoredCountAndProgressToLateSubscriber() {
            using var generator = new DocumentGeneratorService();
            generator.Deserialize(new JObject {
                ["documentQuantity"] = 7,
                ["currentPoints"] = 4f
            });
            using var viewModel = new DocumentTokenViewModel(generator);
            int quantity = -1;
            float progress = -1f;

            using IDisposable quantitySubscription = viewModel.QuantityChanged.Subscribe(value => quantity = value);
            using IDisposable progressSubscription = viewModel.ProgressChanged.Subscribe(value => progress = value);

            Assert.That(quantity, Is.EqualTo(7));
            Assert.That(progress, Is.EqualTo(0.4f).Within(0.0001f));
        }

        [Test]
        public void WalletDeserialize_InvalidDataIsAtomic() {
            using var wallet = new WalletService();
            wallet.ReplenishWallet(new Utils.Value(25));
            JToken before = wallet.Serialize();

            Assert.Throws<JsonSerializationException>(() => wallet.Deserialize(new JObject {
                ["stored"] = -1,
                ["degree"] = 0
            }));

            Assert.That(JToken.DeepEquals(wallet.Serialize(), before), Is.True);
        }

        [Test]
        public void DocumentGeneratorDeserialize_InvalidDataIsAtomic() {
            using var generator = new DocumentGeneratorService();
            generator.Deserialize(new JObject {
                ["documentQuantity"] = 3,
                ["currentPoints"] = 2f
            });
            JToken before = generator.Serialize();

            Assert.Throws<JsonSerializationException>(() => generator.Deserialize(new JObject {
                ["documentQuantity"] = 9,
                ["currentPoints"] = 10f
            }));

            Assert.That(JToken.DeepEquals(generator.Serialize(), before), Is.True);
        }

        private sealed class FakeSaveable : IService, ISaveable {
            public string SaveId { get; }
            public int State { get; set; }
            public bool ThrowOnSerialize { get; set; }

            public FakeSaveable(string saveId, int state) {
                SaveId = saveId;
                State = state;
            }

            public JToken Serialize() {
                if (ThrowOnSerialize) throw new InvalidOperationException("Serialization failed for test.");
                return new JValue(State);
            }

            public void Deserialize(JToken state) {
                if (state?.Type != JTokenType.Integer) {
                    throw new JsonSerializationException("Expected an integer state.");
                }

                int restored = state.Value<int>();
                State = restored;
            }

            public void Dispose() { }
        }
    }
}
