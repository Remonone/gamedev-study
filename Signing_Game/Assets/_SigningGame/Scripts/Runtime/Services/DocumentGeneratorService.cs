using Cysharp.Threading.Tasks;
using Data.Cache;
using R3;
using Services.Locator;
using UnityEngine;

namespace Services {
    public class DocumentGeneratorService : IService, IInitialize, IPostInitialize {

        private PlayerStatStash _stash;

        private const float RequiredPointsForDocument = 10f;

        private float _currentPoint;
        private int _documentQuantity = 1;

        private readonly ReactiveProperty<float> _currentProgress = new();

        public ReadOnlyReactiveProperty<float> CurrentProgress => _currentProgress;

        private IReadOnlyCacheData<GenerationEntries> _generatorCache;

        private readonly Subject<int> _documentCount = new();
        private readonly Subject<Unit> _documentAdded = new();

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
                _documentCount.OnNext(_documentQuantity);
                _documentAdded.OnNext(Unit.Default);
            }

            _currentProgress.Value = _currentPoint / RequiredPointsForDocument;
        }

        public bool TryObtainDocument() {
            if (_documentQuantity < 1) {
                return false;
            }

            _documentCount.OnNext(--_documentQuantity);
            return true;
        }
    }
}
