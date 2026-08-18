using System;
using System.Collections.Generic;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using R3;
using Services.Locator;
using UnityEngine;

namespace Services {
    public readonly struct GenerationWork {
        public double DeltaPoints { get; }
        public double TokenPerSecond { get; }

        public GenerationWork(double deltaPoints, double tokenPerSecond) {
            DeltaPoints = deltaPoints;
            TokenPerSecond = tokenPerSecond;
        }
    }

    public class DocumentGeneratorService : IService, IInitialize, IPostInitialize, ISaveable {

        private PlayerStatStash _stash;

        public const float PointsPerDocument = 10f;

        private double _currentPoint;
        private int _documentQuantity = 1;
        private int _reservationEpoch;
        private long _nextReservationId;
        private readonly HashSet<long> _activeReservations = new();

        private readonly ReactiveProperty<float> _currentProgress = new();

        public ReadOnlyReactiveProperty<float> CurrentProgress => _currentProgress;

        private IReadOnlyCacheData<GenerationEntries> _generatorCache;

        private readonly ReactiveProperty<int> _documentCount = new(1);
        private readonly Subject<Unit> _documentAdded = new();
        private readonly Subject<GenerationWork> _generationWork = new();
        private readonly Subject<int> _documentsGenerated = new();
        private readonly Subject<int> _documentsConsumed = new();

        public string SaveId => "document_generator";

        public Observable<int> DocumentCount => _documentCount;
        public int DocumentQuantity => _documentQuantity;

        public Observable<Unit> DocumentAdded => _documentAdded;
        public Observable<GenerationWork> WorkGenerated => _generationWork;
        public Observable<int> DocumentsGenerated => _documentsGenerated;
        public Observable<int> DocumentsConsumed => _documentsConsumed;

        private readonly CompositeDisposable _disposables = new();

        public void Dispose() {
            InvalidateReservations();
            _currentProgress.Dispose();
            _documentCount.Dispose();
            _documentAdded.Dispose();
            _generationWork.Dispose();
            _documentsGenerated.Dispose();
            _documentsConsumed.Dispose();
            _disposables.Dispose();
        }

        public UniTask InitializeAsync(IServiceScope scope) {
            _stash = scope.Get<PlayerStatStash>();
            _generatorCache = _stash.GenerationData;
            return UniTask.CompletedTask;
        }

        public UniTask PostInitializeAsync(IServiceScope scope) {
            Observable.EveryUpdate().Select(_ => Time.deltaTime).Subscribe(OnUpdate).AddTo(_disposables);
            return UniTask.CompletedTask;
        }

        private void OnUpdate(float dt) {
            var tokenPerSecond = _generatorCache.Value.TokenPerSecond;
            double deltaPoints = Math.Max(0d, (double)dt * tokenPerSecond);
            if (deltaPoints > 0d && !double.IsInfinity(deltaPoints) && !double.IsNaN(deltaPoints)) {
                _generationWork.OnNext(new GenerationWork(deltaPoints, tokenPerSecond));
            }
            double totalPoints = _currentPoint + deltaPoints;
            double generatedDocuments = Math.Floor(totalPoints / PointsPerDocument);
            _currentPoint = totalPoints % PointsPerDocument;

            if (generatedDocuments >= 1d) {
                int tokensPerIncome = Math.Max(0, _generatorCache.Value.TokenPerIncome);
                long storedAndReserved = (long)_documentQuantity + _activeReservations.Count;
                long availableCapacity = Math.Max(0L, int.MaxValue - storedAndReserved);
                double requested = generatedDocuments * tokensPerIncome;
                int actualAdded = requested >= availableCapacity
                    ? (int)availableCapacity
                    : (int)requested;
                // Work converted beyond the integer inventory limit is intentionally discarded.
                if (actualAdded > 0) {
                    _documentQuantity += actualAdded;
                    _documentCount.Value = _documentQuantity;
                    _documentAdded.OnNext(Unit.Default);
                    _documentsGenerated.OnNext(actualAdded);
                }
            }

            _currentProgress.Value = (float)(_currentPoint / PointsPerDocument);
        }

        public bool TryObtainDocument() {
            if (_documentQuantity < 1) {
                return false;
            }

            _documentCount.Value = --_documentQuantity;
            _documentsConsumed.OnNext(1);
            return true;
        }

        internal bool TryReserveDocument(out DocumentReservation reservation) {
            reservation = default;
            if (_documentQuantity < 1) return false;

            long id = ++_nextReservationId;
            _activeReservations.Add(id);
            _documentCount.Value = --_documentQuantity;
            reservation = new DocumentReservation(_reservationEpoch, id);
            return true;
        }

        internal bool TryCommitReservation(DocumentReservation reservation) {
            if (reservation.Epoch != _reservationEpoch || !_activeReservations.Remove(reservation.Id)) {
                return false;
            }

            _documentsConsumed.OnNext(1);
            return true;
        }

        internal bool TryCancelReservation(DocumentReservation reservation) {
            if (reservation.Epoch != _reservationEpoch || !_activeReservations.Remove(reservation.Id)) {
                return false;
            }

            _documentCount.Value = ++_documentQuantity;
            return true;
        }

        public JToken Serialize() {
            long persistedQuantity = (long)_documentQuantity + _activeReservations.Count;
            return new JObject {
                ["documentQuantity"] = persistedQuantity,
                ["currentPoints"] = _currentPoint
            };
        }

        public void Deserialize(JToken state) {
            if (state is not JObject data || data["documentQuantity"]?.Type != JTokenType.Integer ||
                !TryReadNumber(data["currentPoints"], out double currentPoints)) {
                throw new JsonSerializationException(
                    "Document generator save data is missing an integer quantity or numeric current points value.");
            }

            long persistedQuantity = data["documentQuantity"].Value<long>();
            bool invalidPoints = double.IsNaN(currentPoints) || double.IsInfinity(currentPoints) ||
                                   currentPoints < 0f || currentPoints >= PointsPerDocument;
            if (persistedQuantity < 0L || persistedQuantity > int.MaxValue || invalidPoints) {
                throw new JsonSerializationException("Document generator save data contains values outside valid ranges.");
            }

            InvalidateReservations();
            _documentQuantity = (int)persistedQuantity;
            _currentPoint = currentPoints;
            _documentCount.Value = _documentQuantity;
            _currentProgress.Value = (float)(_currentPoint / PointsPerDocument);
        }

        private static bool TryReadNumber(JToken token, out double value) {
            if (token?.Type is JTokenType.Integer or JTokenType.Float) {
                value = token.Value<double>();
                return true;
            }

            value = default;
            return false;
        }

        private void InvalidateReservations() {
            _reservationEpoch++;
            _activeReservations.Clear();
        }

        internal readonly struct DocumentReservation {
            internal int Epoch { get; }
            internal long Id { get; }

            internal DocumentReservation(int epoch, long id) {
                Epoch = epoch;
                Id = id;
            }
        }
    }
}
