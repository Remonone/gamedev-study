using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Contracts;
using Data.Formulas;
using Data.Persistence;
using Data.Upgrades;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Services;
using Services.Locator;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;

namespace Tests.PlayMode {
    public sealed class MetaResetLifecycleTests {
        [UnityTest]
        public IEnumerator CommittedReset_ContinueLoadRestoresOnlyMetaAndSignature() {
            string directory = Path.Combine(Path.GetTempPath(), $"SigningGame_MetaReset_{Guid.NewGuid():N}");
            string path = Path.Combine(directory, "save.json");
            var definition = ScriptableObject.CreateInstance<MetaUpgradeNodeDefinition>();
            definition.Id = "persistent_meta";
            definition.Name = "Persistent Meta";
            definition.MaxLevel = 1;
            definition.CostFormula = new ConstantValue { Value = new Value(5d) };
            definition.Modifiers = Array.Empty<Data.Modifiers.ModifierDefinition>();
            definition.Children = Array.Empty<UpgradeNodeLink>();
            definition.FeatureUnlockIds = Array.Empty<string>();
            definition.StatisticRequirements = Array.Empty<GameStatisticRequirement>();

            try {
                using (var sourceMeta = new MetaProgressionService()) {
                    sourceMeta.BuildDefinitions(new[] { definition });
                    sourceMeta.Deserialize(new JObject {
                        ["bankedPoints"] = 0,
                        ["previousIterationPoints"] = 0,
                        ["moneyPeakStored"] = 92.2d,
                        ["moneyPeakDegree"] = 2,
                        ["upgrades"] = new JArray()
                    });
                    Assert.That(sourceMeta.TryCreatePurchasedState(definition.Id, out JToken purchased, out _), Is.True);
                    var reset = new SaveSnapshot(SaveSnapshot.CurrentVersion, new Dictionary<string, JToken> {
                        ["signature_progression"] = new JObject { ["active"] = "starter_signature" },
                        [MetaProgressionService.SaveSectionId] = purchased
                    });
                    var writer = new SaveService(path, loadExistingOnInitialize: false);
                    Assert.That(writer.SaveSnapshotToFile(reset), Is.True);
                }

                using var scope = new ServiceScope(null);
                var reader = new SaveService(path);
                var signature = new JsonSaveable("signature_progression", new JObject());
                var wallet = new WalletService();
                wallet.ReplenishWallet(new Value(123d));
                wallet.Deserialize(new JObject { ["stored"] = 0d, ["degree"] = 0 });
                var restoredMeta = new MetaProgressionService();
                restoredMeta.BuildDefinitions(new[] { definition });
                scope.Register(reader).Register(signature).Register(wallet).Register(restoredMeta);
                reader.PreInitializeAsync(scope).GetAwaiter().GetResult();

                Assert.That(signature.State["active"]?.Value<string>(), Is.EqualTo("starter_signature"));
                Assert.That(restoredMeta.GetUpgrade(definition.Id).Level, Is.EqualTo(1));
                Assert.That(restoredMeta.PreviousIterationPoints, Is.EqualTo(5));
                Assert.That(restoredMeta.MoneyPeak.IsZero, Is.True);
                Assert.That(wallet.CurrentBalance.IsZero, Is.True);
            } finally {
                UnityEngine.Object.Destroy(definition);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }

            yield return null;
        }

        private sealed class JsonSaveable : IService, ISaveable {
            public string SaveId { get; }
            public JToken State { get; private set; }

            public JsonSaveable(string saveId, JToken state) {
                SaveId = saveId;
                State = state;
            }

            public JToken Serialize() => State.DeepClone();
            public void Deserialize(JToken state) => State = state.DeepClone();
            public void Dispose() { }
        }
    }
}
