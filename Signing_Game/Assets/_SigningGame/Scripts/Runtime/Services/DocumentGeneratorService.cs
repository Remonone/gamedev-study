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

        private float _currentPoint;
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

        public string SaveId => "document_generator";

        public Observable<int> DocumentCount => _documentCount;
        public int DocumentQuantity => _documentQuantity;

        public Observable<Unit> DocumentAdded => _documentAdded;
        public Observable<GenerationWork> WorkGenerated => _generationWork;
        public Observable<int> DocumentsGenerated => _documentsGenerated;

        private readonly CompositeDisposable _disposables = new();

        public void Dispose() {
            InvalidateReservations();
            _currentProgress.Dispose();
            _documentCount.Dispose();
            _documentAdded.Dispose();
            _generationWork.Dispose();
            _documentsGenerated.Dispose();
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
            _currentPoint += (float)deltaPoints;

            int generatedDocuments = Mathf.FloorToInt(_currentPoint / PointsPerDocument);

            if (generatedDocuments > 0) {
                _currentPoint -= generatedDocuments * PointsPerDocument;
                _documentQuantity += generatedDocuments;
                _documentCount.Value = _documentQuantity;
                _documentAdded.OnNext(Unit.Default);
                _documentsGenerated.OnNext(generatedDocuments);
            }

            _currentProgress.Value = _currentPoint / PointsPerDocument;
        }

        public bool TryObtainDocument() {
            if (_documentQuantity < 1) {
                return false;
            }

            _documentCount.Value = --_documentQuantity;
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
            return reservation.Epoch == _reservationEpoch && _activeReservations.Remove(reservation.Id);
        }

        internal bool TryCancelReservation(DocumentReservation reservation) {
            if (reservation.Epoch != _reservationEpoch || !_activeReservations.Remove(reservation.Id)) {
                return false;
            }

            _documentCount.Value = ++_documentQuantity;
            return true;
        }

        public JToken Serialize() {
            return new JObject {
                ["documentQuantity"] = _documentQuantity + _activeReservations.Count,
                ["currentPoints"] = _currentPoint
            };
        }

        public void Deserialize(JToken state) {
            if (state is not JObject data || data["documentQuantity"]?.Type != JTokenType.Integer ||
                !TryReadNumber(data["currentPoints"], out float currentPoints)) {
                throw new JsonSerializationException(
                    "Document generator save data is missing an integer quantity or numeric current points value.");
            }

            int documentQuantity = data["documentQuantity"].Value<int>();
            bool invalidPoints = float.IsNaN(currentPoints) || float.IsInfinity(currentPoints) ||
                                  currentPoints < 0f || currentPoints >= PointsPerDocument;
            if (documentQuantity < 0 || invalidPoints) {
                throw new JsonSerializationException("Document generator save data contains values outside valid ranges.");
            }

            InvalidateReservations();
            _documentQuantity = documentQuantity;
            _currentPoint = currentPoints;
            _documentCount.Value = _documentQuantity;
            _currentProgress.Value = _currentPoint / PointsPerDocument;
        }

        private static bool TryReadNumber(JToken token, out float value) {
            if (token?.Type is JTokenType.Integer or JTokenType.Float) {
                value = token.Value<float>();
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
