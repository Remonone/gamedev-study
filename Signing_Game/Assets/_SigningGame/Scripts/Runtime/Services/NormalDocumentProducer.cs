using System;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Documents;
using Data.Enums;
using Data.Results;
using Data.Rules;
using Services.Locator;
using R3;
using Utils;
using Utils.Text.Generator;

namespace Services {
    public sealed class NormalDocumentProducer : IService, IInitialize, IDocumentProducer {
        private static readonly IDocumentEvaluationPolicy Policy = new PlayerDocumentEvaluationPolicy();
        private static readonly ulong FallbackCriticalRandomSeed =
            SeedUtility.FromString(nameof(NormalDocumentProducer));
        private const double MaximumValueLog10 = (double)int.MaxValue * 3d;

        private DocumentGeneratorService _generator;
        private IMoneyAggregator _aggregator;
        private IReadOnlyCacheData<IncomeEntries> _incomeData;
        private IReadOnlyCacheData<DocumentEntries> _documentData;
        private AcceptedNormalDocumentService _acceptedDocuments;
        private SignatureCriticalRandomService _criticalRandom;
        private Observable<Unit> _offersChanged;

        public int Priority => 0;
        public Observable<Unit> OffersChanged => _offersChanged;

        public UniTask InitializeAsync(IServiceScope scope) {
            _generator = scope.Get<DocumentGeneratorService>();
            _aggregator = scope.Get<IMoneyAggregator>();
            PlayerStatStash stash = scope.Get<PlayerStatStash>();
            _incomeData = stash.IncomeData;
            _documentData = stash.Documents;
            scope.TryGet(out _acceptedDocuments);
            if (!scope.TryGet(out _criticalRandom)) {
                _criticalRandom = new SignatureCriticalRandomService(FallbackCriticalRandomSeed);
            }
            _offersChanged = _generator.DocumentCount.Select(_ => Unit.Default);
            return UniTask.CompletedTask;
        }

        public bool TryProduce(out IDocumentSession session) {
            TryPeekOffer(out DocumentOffer offer);
            return TryProduce(offer.Key, out session);
        }

        public bool TryPeekOffer(out DocumentOffer offer) {
            offer = new DocumentOffer(
                new DocumentOfferKey(DocumentKind.Normal, "normal"),
                _generator.DocumentQuantity > 0);
            return true;
        }

        public bool TryProduce(DocumentOfferKey offerKey, out IDocumentSession session) {
            session = null;
            if (offerKey.Kind != DocumentKind.Normal ||
                !string.Equals(offerKey.DomainId, "normal", StringComparison.Ordinal)) {
                return false;
            }

            if (!_generator.TryReserveDocument(out DocumentGeneratorService.DocumentReservation reservation)) {
                return false;
            }

            session = new NormalDocumentSession(
                _generator,
                reservation,
                _aggregator,
                _incomeData,
                _documentData,
                _acceptedDocuments,
                _criticalRandom);
            return true;
        }

        public void Dispose() { }

        private sealed class PlayerDocumentEvaluationPolicy : IDocumentEvaluationPolicy {
            public DocumentEvaluationInputs Resolve(SignatureDifficultyContext difficulty) {
                return new DocumentEvaluationInputs(difficulty.EffectiveDifficulty, SignatureRuleModifiers.None);
            }
        }

        private sealed class NormalDocumentSession : IDocumentSession {
            private readonly DocumentGeneratorService _generator;
            private readonly DocumentGeneratorService.DocumentReservation _reservation;
            private readonly IMoneyAggregator _aggregator;
            private readonly IReadOnlyCacheData<IncomeEntries> _incomeData;
            private readonly IReadOnlyCacheData<DocumentEntries> _documentData;
            private readonly AcceptedNormalDocumentService _acceptedDocuments;
            private readonly SignatureCriticalRandomService _criticalRandom;
            private bool _finished;

            public DocumentKind Kind => DocumentKind.Normal;
            public IDocumentEvaluationPolicy EvaluationPolicy => Policy;

            public NormalDocumentSession(
                DocumentGeneratorService generator,
                DocumentGeneratorService.DocumentReservation reservation,
                IMoneyAggregator aggregator,
                IReadOnlyCacheData<IncomeEntries> incomeData,
                IReadOnlyCacheData<DocumentEntries> documentData,
                AcceptedNormalDocumentService acceptedDocuments,
                SignatureCriticalRandomService criticalRandom) {
                _generator = generator;
                _reservation = reservation;
                _aggregator = aggregator;
                _incomeData = incomeData;
                _documentData = documentData;
                _acceptedDocuments = acceptedDocuments;
                _criticalRandom = criticalRandom ?? throw new ArgumentNullException(nameof(criticalRandom));
            }

            public bool TryProcess(SignatureEvaluationResult result) {
                if (result == null) throw new ArgumentNullException(nameof(result));
                if (_finished || !_generator.TryCommitReservation(_reservation)) return false;

                _finished = true;
                if (result.Status == SignatureEvaluationStatus.Accepted) {
                    int selectedQuality = Math.Clamp(
                        _documentData.Value.SelectedDocumentQualityLevel,
                        0,
                        9);
                    SendReward(result);
                    _acceptedDocuments?.Report(
                        NormalDocumentProcessingSource.Manual,
                        selectedQuality,
                        result.Similarity);
                }
                return true;
            }

            public void Dispose() {
                if (_finished) return;
                _finished = true;
                _generator.TryCancelReservation(_reservation);
            }

            private void SendReward(SignatureEvaluationResult result) {
                IncomeEntries income = _incomeData.Value;
                double accuracyBonus = Math.Min(
                    Math.Max(result.Similarity * income.MinMultiplyScale, 1d),
                    income.MaxMultiplicationScale);
                Value baseReward = MultiplyValueSafely(income.IncomePerDocument, accuracyBonus);
                int guaranteedExtra = MultiPayUtility.SplitChance(
                    income.ManualSignatureMultiPayChance,
                    out float extraChance);
                int paymentCount = 1 + guaranteedExtra;
                if (extraChance > 0f && _criticalRandom.RollManual(extraChance)) paymentCount++;

                for (int paymentIndex = 0; paymentIndex < paymentCount; paymentIndex++) {
                    Value reward = baseReward;
                    if (_criticalRandom.RollManual(income.ManualSignatureCriticalChance)) {
                        reward = MultiplyValueSafely(
                            reward,
                            SignatureCriticalRandomService.NormalizeMultiplier(
                                income.ManualSignatureCriticalMultiplier));
                    }

                    _aggregator.AddMoney(reward);
                }
            }

            private static Value MultiplyValueSafely(Value value, double multiplier) {
                if (value.IsZero || multiplier <= 0d) return Value.Zero;
                if (value.Base.Degree == int.MaxValue) return Value.Infinity;

                double resultLog10 = value.ToLog10() + Math.Log10(multiplier);
                if (double.IsNaN(resultLog10)) return Value.Zero;
                if (double.IsPositiveInfinity(resultLog10) || resultLog10 >= MaximumValueLog10) {
                    return Value.Infinity;
                }

                return Value.FromLog10(resultLog10);
            }
        }
    }
}
