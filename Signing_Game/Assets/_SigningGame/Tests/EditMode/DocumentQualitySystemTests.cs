using System;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Documents;
using Data.Enums;
using Data.Results;
using NUnit.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using R3;
using Services;
using Services.Locator;

namespace Tests.EditMode {
    public sealed class DocumentQualitySystemTests {
        [Test]
        public void SaveRoundTripKeepsPendingSelectionAndRejectsMalformedValuesAtomically() {
            using var service = new DocumentQualityService();
            service.Deserialize(new JObject { ["selectedLevel"] = 7 });

            Assert.That(service.Serialize()["selectedLevel"]?.Value<int>(), Is.EqualTo(7));
            Assert.Throws<JsonSerializationException>(() => service.Deserialize(new JObject {
                ["selectedLevel"] = 10
            }));
            Assert.That(service.Serialize()["selectedLevel"]?.Value<int>(), Is.EqualTo(7));
            Assert.Throws<JsonSerializationException>(() => service.Deserialize(new JObject {
                ["selectedLevel"] = "bad"
            }));
            Assert.That(service.Serialize()["selectedLevel"]?.Value<int>(), Is.EqualTo(7));
        }

        [Test]
        public void PostInitializeClampsOnlyAfterComputedMaximumIsAvailable() {
            using TestEnvironment environment = CreateEnvironment(maximumQuality: 2, restoredSelection: 7);

            Assert.That(environment.Quality.SelectedQualityLevel, Is.EqualTo(2));
            Assert.That(environment.Stash.Documents.Value.SelectedDocumentQualityLevel, Is.EqualTo(2));
            Assert.That(environment.Cache.GetVersion<DocumentEntries>(), Is.EqualTo(1));
        }

        [Test]
        public void SelectionInvalidatesDocumentAndDependentSignatureCachesOnce() {
            using TestEnvironment environment = CreateEnvironment(maximumQuality: 3);
            environment.ResetInvalidationCount();

            Assert.That(environment.Quality.SetSelection(2), Is.True);

            Assert.That(environment.DocumentInvalidations, Is.EqualTo(1));
            Assert.That(environment.Cache.GetVersion<SignatureEntries>(), Is.EqualTo(1));
            Assert.That(environment.Stash.Documents.Value.SelectedDocumentQualityLevel, Is.EqualTo(2));
        }

        [Test]
        public void MaximumDecreaseGuardedReinvalidatesAndClampsComputedCache() {
            using TestEnvironment environment = CreateEnvironment(maximumQuality: 3, restoredSelection: 3);
            environment.ResetInvalidationCount();
            environment.DocumentCalculator.MaximumQuality = 1;

            ((ICacheInvalidator)environment.Cache).Invalidate<DocumentEntries>();

            Assert.That(environment.DocumentInvalidations, Is.EqualTo(2));
            Assert.That(environment.Quality.SelectedQualityLevel, Is.EqualTo(1));
            Assert.That(environment.Stash.Documents.Value.DocumentQualityLevel, Is.EqualTo(1));
            Assert.That(environment.Stash.Documents.Value.SelectedDocumentQualityLevel, Is.EqualTo(1));
        }

        [Test]
        public void GuidanceProducerRequestIsIdempotentAndSessionHasNoEconomyEffect() {
            using var producer = new SignatureGuidanceDocumentProducer();

            Assert.That(producer.Request(), Is.True);
            Assert.That(producer.Request(), Is.False);
            Assert.That(producer.TryPeekOffer(out DocumentOffer offer), Is.True);
            Assert.That(offer.Key.Kind, Is.EqualTo(DocumentKind.SignatureGuidance));
            Assert.That(producer.TryProduce(offer.Key, out IDocumentSession session), Is.True);
            Assert.That(producer.TryPeekOffer(out _), Is.False);

            var result = new SignatureEvaluationResult(
                SignatureEvaluationStatus.Accepted,
                SignatureFailureReason.None,
                0.82f,
                0.5f,
                new SignatureScoreBreakdown(0.8f, 0.8f, 0.8f, 0.8f, 0.8f));
            Assert.That(session.TryProcess(result), Is.True);
            Assert.That(session.TryProcess(result), Is.False);
            session.Dispose();
        }

        [Test]
        public void GuidanceReminderUsesProgressivePhase() {
            SignatureGuidancePhase phase = SignatureGuidancePhaseCalculator.Calculate(0d, 1, 0);

            Assert.That(phase.Kind, Is.EqualTo(SignatureGuidancePhaseKind.Progressive));
            Assert.That(phase.Alpha, Is.EqualTo(SignatureGuidancePhase.MaximumAlpha));
        }

        private static TestEnvironment CreateEnvironment(int maximumQuality, int restoredSelection = 0) {
            var scope = new ServiceScope(null);
            var cache = new CacheVersionService();
            var quality = new DocumentQualityService();
            var stash = new PlayerStatStash();
            var documentCalculator = new QualityAwareDocumentCalculator(quality, maximumQuality);

            scope.Register(cache, typeof(ICacheVersionProvider), typeof(ICacheInvalidator));
            scope.Register(quality);
            scope.Register<ICacheCalculator<IncomeEntries>>(new StaticCalculator<IncomeEntries>(default));
            scope.Register<ICacheCalculator<SignatureEntries>>(new StaticCalculator<SignatureEntries>(default));
            scope.Register<ICacheCalculator<GenerationEntries>>(new StaticCalculator<GenerationEntries>(default));
            scope.Register<ICacheCalculator<OfficeEntries>>(new StaticCalculator<OfficeEntries>(default));
            scope.Register<ICacheCalculator<DocumentEntries>>(documentCalculator);
            scope.Register(stash);

            if (restoredSelection != 0) {
                quality.Deserialize(new JObject { ["selectedLevel"] = restoredSelection });
            }

            stash.PreInitializeAsync(scope).GetAwaiter().GetResult();
            quality.PostInitializeAsync(scope).GetAwaiter().GetResult();

            var environment = new TestEnvironment(scope, cache, quality, stash, documentCalculator);
            environment.ResetInvalidationCount();
            return environment;
        }

        private sealed class TestEnvironment : IDisposable {
            public ServiceScope Scope { get; }
            public CacheVersionService Cache { get; }
            public DocumentQualityService Quality { get; }
            public PlayerStatStash Stash { get; }
            public QualityAwareDocumentCalculator DocumentCalculator { get; }
            public int DocumentInvalidations { get; private set; }
            private readonly IDisposable _invalidationSubscription;

            public TestEnvironment(
                ServiceScope scope,
                CacheVersionService cache,
                DocumentQualityService quality,
                PlayerStatStash stash,
                QualityAwareDocumentCalculator documentCalculator) {
                Scope = scope;
                Cache = cache;
                Quality = quality;
                Stash = stash;
                DocumentCalculator = documentCalculator;
                _invalidationSubscription = Cache.Invalidated.Subscribe(type => {
                    if (type == typeof(DocumentEntries)) DocumentInvalidations++;
                });
            }

            public void ResetInvalidationCount() => DocumentInvalidations = 0;

            public void Dispose() {
                _invalidationSubscription.Dispose();
                Scope.Dispose();
            }
        }

        private sealed class QualityAwareDocumentCalculator : ICacheCalculator<DocumentEntries>, IService {
            private readonly DocumentQualityService _quality;

            public int MaximumQuality { get; set; }

            public QualityAwareDocumentCalculator(DocumentQualityService quality, int maximumQuality) {
                _quality = quality;
                MaximumQuality = maximumQuality;
            }

            public DocumentEntries Calculate() {
                return new DocumentEntries {
                    DocumentQualityLevel = MaximumQuality,
                    SelectedDocumentQualityLevel = _quality.SelectedQualityLevel
                };
            }

            public void Dispose() { }
        }

        private sealed class StaticCalculator<T> : ICacheCalculator<T>, IService where T : struct {
            private readonly T _value;

            public StaticCalculator(T value) => _value = value;
            public T Calculate() => _value;
            public void Dispose() { }
        }
    }
}
