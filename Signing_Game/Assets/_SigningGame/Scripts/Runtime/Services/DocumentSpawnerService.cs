using Cysharp.Threading.Tasks;
using Data.Cache;
using Presentation;
using R3;
using Services.Locator;
using UnityEngine;
using Utils;

namespace Services {
    public class DocumentSpawnerService : IService, IInitialize, IPostInitialize {

        private DocumentDispenser _dispenser;
        private DocumentGeneratorService _generator;
        private PlayerStatStash _stash;
        private IReadOnlyCacheData<GenerationEntries> _generationData;
        
        private Cooldown _dispenseCooldown;

        private readonly CompositeDisposable _disposables = new();

        public UniTask InitializeAsync(IServiceScope scope) {
            _dispenser = scope.Get<DocumentDispenser>();
            _generator = scope.Get<DocumentGeneratorService>();
            _stash = scope.Get<PlayerStatStash>();
            _generationData = _stash.GenerationData;
            return UniTask.CompletedTask;
        }

        public UniTask PostInitializeAsync(IServiceScope scope) {
            _dispenseCooldown =
                new Cooldown(_generationData.Value.DispenseCooldown, Observable.EveryUpdate().Select(_ => Time.deltaTime));
            _dispenseCooldown.TryStart();
            _dispenseCooldown.Completed.Subscribe(_ => TrySpawn()).AddTo(_disposables);
            _generator.DocumentAdded.Subscribe(_ => TrySpawn()).AddTo(_disposables);
            TrySpawn();
            return UniTask.CompletedTask;
        }

        private void TrySpawn() {
            if (!_dispenseCooldown.IsReady)
                return;

            if (!_generator.TryObtainDocument()) {
                return;
            }

            _dispenser.Spawn();
            _dispenseCooldown.Restart();
        }

        public void Dispose() {
            _disposables.Dispose();
            _dispenseCooldown?.Dispose();
        }
    }
}
