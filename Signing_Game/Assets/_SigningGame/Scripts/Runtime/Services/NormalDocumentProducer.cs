using System;
using System.Globalization;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Documents;
using Data.Enums;
using Data.Results;
using Data.Rules;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using R3;
using Services.Locator;
using Utils;
using Utils.Text.Generator;

namespace Services {
    internal static class DocumentStampRewardMultiplier {
        public static double Resolve(bool requiresStamp, bool isStamped) {
            if (requiresStamp) return isStamped ? 3d : 0.5d;
            return isStamped ? 0.5d : 1d;
        }
    }

    internal static class DocumentQualityRewardMultiplier {
        public static double Resolve(DocumentEntries documents) {
            int level = Math.Clamp(documents.SelectedDocumentQualityLevel, 0, 9) + 1;
            float configured = documents.DocumentQualityIncomeMultiplier;
            double multiplier = !float.IsNaN(configured) && !float.IsInfinity(configured) && configured >= 0f
                ? configured
                : 0d;
            return level + level * multiplier;
        }
    }

    public sealed class NormalDocumentProducer : IService, IInitialize, IDocumentProducer, ISaveable {
        private static readonly IDocumentEvaluationPolicy Policy = new PlayerDocumentEvaluationPolicy();
        private static readonly ulong FallbackCriticalRandomSeed =
            SeedUtility.FromString(nameof(NormalDocumentProducer));
        private const double MaximumValueLog10 = (double)int.MaxValue * 3d;
        private const int DefaultStampInterval = 2;
        private const string NormalDomainId = "normal";
        private const string IdleOfferId = "normal:idle";

        private readonly Subject<Unit> _offersChanged = new();
        private readonly CompositeDisposable _subscriptions = new();

        private DocumentGeneratorService _generator;
        private IMoneyAggregator _aggregator;
        private IReadOnlyCacheData<IncomeEntries> _incomeData;
        private IReadOnlyCacheData<DocumentEntries> _documentData;
        private AcceptedNormalDocumentService _acceptedDocuments;
        private SignatureCriticalRandomService _criticalRandom;
        private UnlockService _unlocks;
        private DocumentOffer _currentOffer;
        private JToken _deferredRestore;
        private long _nextOfferSequence = 1;
        private bool _stampUnlocked;
        private bool _isClaiming;
        private bool _initialized;

        public int Priority => 0;
        public Observable<Unit> OffersChanged => _offersChanged;
        public string SaveId => "normal_document_stamps";

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

            scope.TryGet(out _unlocks);
            _stampUnlocked = _unlocks != null && _unlocks.IsUnlocked(Constants.FeatureIds.Stamp);
            _generator.DocumentCount.Subscribe(_ => OnGeneratorChanged()).AddTo(_subscriptions);
            _unlocks?.Changed.Subscribe(_ => OnUnlockChanged()).AddTo(_subscriptions);

            _initialized = true;
            if (_deferredRestore != null) {
                JToken deferred = _deferredRestore;
                _deferredRestore = null;
                try {
                    ApplyRestore(deferred);
                }
                catch (JsonSerializationException exception) {
                    UnityEngine.Debug.LogWarning(
                        $"Normal document stamp save was ignored: {exception.Message}");
                    ResetOfferState();
                }
            }

            return UniTask.CompletedTask;
        }

        public bool TryProduce(out IDocumentSession session) {
            session = null;
            if (!TryPeekOffer(out DocumentOffer offer)) return false;
            return TryProduce(offer.Key, out session);
        }

        public bool TryPeekOffer(out DocumentOffer offer) {
            offer = null;
            if (_isClaiming || _generator == null) return false;

            bool isAvailable = _generator.DocumentQuantity > 0;
            if (_currentOffer == null) {
                if (!isAvailable) {
                    offer = new DocumentOffer(
                        new DocumentOfferKey(DocumentKind.Normal, IdleOfferId),
                        false);
                    return true;
                }

                _currentOffer = CreateAvailableOffer();
            }
            else if (_currentOffer.IsAvailable != isAvailable) {
                _currentOffer = RecreateOffer(_currentOffer, isAvailable);
            }

            offer = _currentOffer;
            return true;
        }

        public bool TryProduce(DocumentOfferKey offerKey, out IDocumentSession session) {
            session = null;
            if (_isClaiming || offerKey.Kind != DocumentKind.Normal ||
                !TryPeekOffer(out DocumentOffer current) || !current.IsAvailable ||
                !current.Key.Equals(offerKey)) {
                return false;
            }

            DocumentOffer claimedOffer = current;
            _currentOffer = null;
            _isClaiming = true;
            DocumentGeneratorService.DocumentReservation reservation = default;
            bool reserved = false;
            try {
                if (!_generator.TryReserveDocument(out reservation)) {
                    _currentOffer = claimedOffer;
                    return false;
                }

                reserved = true;
                session = new NormalDocumentSession(
                    _generator,
                    reservation,
                    _aggregator,
                    _incomeData,
                    _documentData,
                    _acceptedDocuments,
                    _criticalRandom,
                    claimedOffer.RequiresStamp);
                return true;
            }
            catch {
                _currentOffer = claimedOffer;
                if (reserved) _generator.TryCancelReservation(reservation);
                throw;
            }
            finally {
                _isClaiming = false;
                if (session != null) _offersChanged.OnNext(Unit.Default);
            }
        }

        public JToken Serialize() {
            var data = new JObject {
                ["nextOfferSequence"] = _nextOfferSequence
            };
            if (_currentOffer != null) {
                data["currentOfferSequence"] = GetSequence(_currentOffer);
                data["currentRequiresStamp"] = _currentOffer.RequiresStamp;
            }

            return data;
        }

        public void Deserialize(JToken state) {
            if (state == null) throw new JsonSerializationException("Normal document stamp save is missing.");
            if (!_initialized) {
                _deferredRestore = state.DeepClone();
                return;
            }

            ApplyRestore(state);
        }

        public void Dispose() {
            _subscriptions.Dispose();
            _offersChanged.Dispose();
            _deferredRestore = null;
            _currentOffer = null;
            _generator = null;
            _unlocks = null;
        }

        private void ApplyRestore(JToken state) {
            if (state is not JObject data ||
                !TryReadLong(data["nextOfferSequence"], out long nextSequence) ||
                nextSequence < 1) {
                throw new JsonSerializationException(
                    "Normal document stamp save requires a positive next offer sequence.");
            }

            JToken currentSequenceToken = data["currentOfferSequence"];
            DocumentOffer restoredOffer = null;
            if (currentSequenceToken != null) {
                if (!TryReadLong(currentSequenceToken, out long currentSequence) ||
                    currentSequence < 1 || currentSequence >= nextSequence ||
                    data["currentRequiresStamp"]?.Type != JTokenType.Boolean) {
                    throw new JsonSerializationException(
                        "Normal document stamp save contains an invalid current offer.");
                }

                bool requiresStamp = data["currentRequiresStamp"].Value<bool>();
                if (_stampUnlocked || !requiresStamp) {
                    restoredOffer = new DocumentOffer(
                        new DocumentOfferKey(DocumentKind.Normal, BuildOfferId(currentSequence, requiresStamp)),
                        _generator.DocumentQuantity > 0,
                        requiresStamp: requiresStamp);
                }
            }
            else if (data["currentRequiresStamp"] != null) {
                throw new JsonSerializationException(
                    "Normal document stamp save contains a flag without a current offer.");
            }

            _nextOfferSequence = nextSequence;
            _currentOffer = restoredOffer;
        }

        private void OnGeneratorChanged() {
            if (_isClaiming) return;
            _offersChanged.OnNext(Unit.Default);
        }

        private void OnUnlockChanged() {
            bool unlocked = _unlocks != null && _unlocks.IsUnlocked(Constants.FeatureIds.Stamp);
            if (_stampUnlocked == unlocked) return;

            _stampUnlocked = unlocked;
            _currentOffer = null;
            if (!_isClaiming) _offersChanged.OnNext(Unit.Default);
        }

        private DocumentOffer CreateAvailableOffer() {
            if (_nextOfferSequence == long.MaxValue) {
                throw new InvalidOperationException("Normal document offer sequence is exhausted.");
            }

            long sequence = _nextOfferSequence++;
            int interval = ResolveStampInterval();
            bool requiresStamp = _stampUnlocked && sequence % interval == 0;
            return new DocumentOffer(
                new DocumentOfferKey(DocumentKind.Normal, BuildOfferId(sequence, requiresStamp)),
                true,
                requiresStamp: requiresStamp);
        }

        private static DocumentOffer RecreateOffer(DocumentOffer offer, bool isAvailable) {
            return new DocumentOffer(
                offer.Key,
                isAvailable,
                offer.Header,
                offer.Icon,
                offer.PersonName,
                offer.PersonAge,
                offer.Amount,
                offer.InternalMultiplier,
                offer.RequiresStamp);
        }

        private int ResolveStampInterval() {
            int configured = _documentData?.Value.StampRequiredEveryNthOffer ?? DefaultStampInterval;
            return configured > 0 ? configured : DefaultStampInterval;
        }

        private static long GetSequence(DocumentOffer offer) {
            string[] parts = offer.Key.DomainId.Split(':');
            if (parts.Length != 3 || !long.TryParse(
                    parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long sequence) ||
                sequence < 1) {
                throw new InvalidOperationException($"Invalid normal document offer key '{offer.Key.DomainId}'.");
            }

            return sequence;
        }

        private static string BuildOfferId(long sequence, bool requiresStamp) {
            return NormalDomainId + ":" + sequence.ToString(CultureInfo.InvariantCulture) + ":" +
                   (requiresStamp ? "1" : "0");
        }

        private static bool TryReadLong(JToken token, out long value) {
            if (token?.Type == JTokenType.Integer) {
                value = token.Value<long>();
                return true;
            }

            value = default;
            return false;
        }

        private void ResetOfferState() {
            _nextOfferSequence = 1;
            _currentOffer = null;
        }

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
            private readonly bool _requiresStamp;
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
                SignatureCriticalRandomService criticalRandom,
                bool requiresStamp) {
                _generator = generator;
                _reservation = reservation;
                _aggregator = aggregator;
                _incomeData = incomeData;
                _documentData = documentData;
                _acceptedDocuments = acceptedDocuments;
                _criticalRandom = criticalRandom ?? throw new ArgumentNullException(nameof(criticalRandom));
                _requiresStamp = requiresStamp;
            }

            public bool TryProcess(SignatureEvaluationResult result, bool isStamped = false) {
                if (result == null) throw new ArgumentNullException(nameof(result));
                if (_finished || !_generator.TryCommitReservation(_reservation)) return false;

                _finished = true;
                if (result.Status == SignatureEvaluationStatus.Accepted) {
                    int selectedQuality = Math.Clamp(
                        _documentData.Value.SelectedDocumentQualityLevel,
                        0,
                        9);
                    SendReward(result, isStamped);
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

            private void SendReward(SignatureEvaluationResult result, bool isStamped) {
                IncomeEntries income = _incomeData.Value;
                double accuracyBonus = Math.Min(
                    Math.Max(result.Similarity * income.MinMultiplyScale, 1d),
                    income.MaxMultiplicationScale);
                Value baseReward = MultiplyValueSafely(income.IncomePerDocument, accuracyBonus);
                baseReward = MultiplyValueSafely(
                    baseReward,
                    DocumentStampRewardMultiplier.Resolve(_requiresStamp, isStamped));
                baseReward = MultiplyValueSafely(baseReward, DocumentQualityRewardMultiplier.Resolve(_documentData.Value));
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
