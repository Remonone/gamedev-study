using System;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using R3;
using Services.Locator;
using UnityEngine;

namespace Services {
    public class DocumentGeneratorService : IService, IInitialize, IPostInitialize, ISaveable {

        private PlayerStatStash _stash;

        private const float RequiredPointsForDocument = 10f;

        private float _currentPoint;
        private int _documentQuantity = 1;

        private readonly ReactiveProperty<float> _currentProgress = new();

        public ReadOnlyReactiveProperty<float> CurrentProgress => _currentProgress;

        private IReadOnlyCacheData<GenerationEntries> _generatorCache;

        private readonly ReactiveProperty<int> _documentCount = new(1);
        private readonly Subject<Unit> _documentAdded = new();

        public string SaveId => "document_generator";

        public Observable<int> DocumentCount => _documentCount;

        public Observable<Unit> DocumentAdded => _documentAdded;

        private readonly CompositeDisposable _disposables = new();

        public void Dispose() {
            _currentProgress.Dispose();
            _documentCount.Dispose();
            _documentAdded.Dispose();
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
            _currentPoint += dt * tokenPerSecond;

            int generatedDocuments = Mathf.FloorToInt(_currentPoint / RequiredPointsForDocument);

            if (generatedDocuments > 0) {
                _currentPoint -= generatedDocuments * RequiredPointsForDocument;
                _documentQuantity += generatedDocuments;
                _documentCount.Value = _documentQuantity;
                _documentAdded.OnNext(Unit.Default);
            }

            _currentProgress.Value = _currentPoint / RequiredPointsForDocument;
        }

        public bool TryObtainDocument() {
            if (_documentQuantity < 1) {
                return false;
            }

            _documentCount.Value = --_documentQuantity;
            return true;
        }

        public JToken Serialize() {
            return new JObject {
                ["documentQuantity"] = _documentQuantity,
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
                                 currentPoints < 0f || currentPoints >= RequiredPointsForDocument;
            if (documentQuantity < 0 || invalidPoints) {
                throw new JsonSerializationException("Document generator save data contains values outside valid ranges.");
            }

            _documentQuantity = documentQuantity;
            _currentPoint = currentPoints;
            _documentCount.Value = _documentQuantity;
            _currentProgress.Value = _currentPoint / RequiredPointsForDocument;
        }

        private static bool TryReadNumber(JToken token, out float value) {
            if (token?.Type is JTokenType.Integer or JTokenType.Float) {
                value = token.Value<float>();
                return true;
            }

            value = default;
            return false;
        }
    }
}
