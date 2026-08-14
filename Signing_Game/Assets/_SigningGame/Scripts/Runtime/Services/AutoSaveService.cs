using System;
using Cysharp.Threading.Tasks;
using R3;
using Services.Locator;
using UnityEngine;

namespace Services {
    public sealed class AutoSaveService : IService, IPostInitialize {
        private const float DefaultIntervalSeconds = 5f * 60f;

        private readonly SaveService _saveService;
        private readonly float _intervalSeconds;
        private readonly CompositeDisposable _disposables = new();

        private float _elapsedSeconds;
        private bool _subscribedToQuitting;
        private bool _suspended;

        public bool IsSuspended => _suspended;

        public AutoSaveService(SaveService saveService, float intervalSeconds = DefaultIntervalSeconds) {
            _saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
            if (float.IsNaN(intervalSeconds) || float.IsInfinity(intervalSeconds) || intervalSeconds <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds), "Autosave interval must be finite and positive.");
            }

            _intervalSeconds = intervalSeconds;
        }

        public UniTask PostInitializeAsync(IServiceScope scope) {
            Application.quitting += OnApplicationQuitting;
            _subscribedToQuitting = true;
            Observable.EveryUpdate().Subscribe(OnUpdate).AddTo(_disposables);
            return UniTask.CompletedTask;
        }

        public void Dispose() {
            if (_subscribedToQuitting) {
                Application.quitting -= OnApplicationQuitting;
                _subscribedToQuitting = false;
            }

            _disposables.Dispose();
        }

        public void Suspend() => _suspended = true;

        public void Resume() => _suspended = false;

        private void OnUpdate(Unit _) {
            if (_suspended) return;
            _elapsedSeconds += Time.unscaledDeltaTime;
            if (_elapsedSeconds < _intervalSeconds) return;

            _elapsedSeconds %= _intervalSeconds;
            _saveService.SaveToFile();
        }

        private void OnApplicationQuitting() {
            if (_suspended) return;
            _saveService.SaveToFile();
        }
    }
}
