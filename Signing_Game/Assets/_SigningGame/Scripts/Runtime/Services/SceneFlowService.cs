using System;
using Constants;
using Cysharp.Threading.Tasks;
using R3;
using Services.Locator;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Services {
    public sealed class SceneFlowService : IService {
        private readonly GameSessionService _session;
        private readonly ReactiveProperty<bool> _loading = new(false);
        private readonly ReactiveProperty<float> _progress = new(0f);
        private readonly ReactiveProperty<string> _lastError = new(string.Empty);

        private bool _transitionInProgress;
        private int _nextReloadReservation;
        private int _activeReloadReservation;

        public Observable<bool> Loading => _loading;
        public Observable<float> Progress => _progress;
        public Observable<string> LastError => _lastError;
        public bool IsLoading => _loading.Value;
        public bool IsTransitionInProgress => _transitionInProgress;

        public SceneFlowService(GameSessionService session) {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public void OpenMainMenu() => LoadMainMenuAsync().Forget();
        public void Play() {
            GameLaunchMode mode = SaveService.HasValidSave()
                ? GameLaunchMode.Continue
                : GameLaunchMode.NewGame;
            if (TryReserveGameReload(mode, out int reservation)) StartReservedGameReload(reservation);
        }
        public void Quit() => Application.Quit();

        public bool TryReserveGameReload(GameLaunchMode mode, out int reservation) {
            reservation = 0;
            if (_transitionInProgress || _session.HasPendingLaunch) return false;
            _transitionInProgress = true;
            try {
                _session.Prepare(mode);
                _activeReloadReservation = ++_nextReloadReservation;
                reservation = _activeReloadReservation;
                return true;
            } catch {
                _transitionInProgress = false;
                _activeReloadReservation = 0;
                throw;
            }
        }

        public bool StartReservedGameReload(int reservation) {
            if (!_transitionInProgress || reservation == 0 || reservation != _activeReloadReservation) return false;
            _activeReloadReservation = 0;
            BeginLoading();
            LoadReservedGameAsync().Forget();
            return true;
        }

        public bool CancelReservedGameReload(int reservation) {
            if (!_transitionInProgress || reservation == 0 || reservation != _activeReloadReservation) return false;
            _activeReloadReservation = 0;
            _session.ClearPending();
            _transitionInProgress = false;
            return true;
        }

        public void RecoverCommittedReloadToMainMenu() {
            _activeReloadReservation = 0;
            _session.ClearPending();
            _transitionInProgress = false;
            OpenMainMenu();
        }

        public void Dispose() {
            _loading.Dispose();
            _progress.Dispose();
            _lastError.Dispose();
        }

        private async UniTaskVoid LoadMainMenuAsync() {
            if (_transitionInProgress) return;
            _transitionInProgress = true;
            BeginLoading();
            try {
                await LoadSceneAsync(InternalConstants.MAIN_MENU_SCENE, false);
                FinishLoading();
            } catch (Exception exception) {
                SetFatalError($"Failed to load the main menu: {exception.Message}", exception);
            } finally {
                _transitionInProgress = false;
            }
        }

        private async UniTaskVoid LoadReservedGameAsync() {
            try {
                await LoadSceneAsync(InternalConstants.GAME_SCENE, true);
                FinishLoading();
            } catch (Exception exception) {
                _session.ClearPending();
                await RecoverMainMenuAsync(exception);
            } finally {
                _transitionInProgress = false;
            }
        }

        private async UniTask LoadSceneAsync(string sceneName, bool waitForSceneScope) {
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null) throw new InvalidOperationException($"Unity could not start loading scene '{sceneName}'.");

            while (!operation.isDone) {
                _progress.Value = Mathf.Clamp01(operation.progress / 0.9f) * (waitForSceneScope ? 0.9f : 1f);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (!waitForSceneScope) {
                _progress.Value = 1f;
                return;
            }

            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded) {
                throw new InvalidOperationException($"Scene '{sceneName}' finished loading but is not available.");
            }

            ServiceLocator locator = ServiceLocator.ForScene(scene);
            await UniTask.WaitUntil(() => locator != null && locator.IsInitializationComplete);
            if (locator.InitializationException != null) {
                throw new InvalidOperationException(
                    $"Scene '{sceneName}' failed to initialize.", locator.InitializationException);
            }

            _progress.Value = 1f;
            await UniTask.Yield(PlayerLoopTiming.Update);
        }

        private async UniTask RecoverMainMenuAsync(Exception originalException) {
            string message = $"Failed to start the game: {originalException.Message}";
            _lastError.Value = message;
            Debug.LogException(originalException);
            try {
                _progress.Value = 0f;
                await LoadSceneAsync(InternalConstants.MAIN_MENU_SCENE, false);
                _loading.Value = false;
                _progress.Value = 0f;
            } catch (Exception recoveryException) {
                SetFatalError($"{message}\nFailed to restore the main menu: {recoveryException.Message}", recoveryException);
            }
        }

        private void BeginLoading() {
            _lastError.Value = string.Empty;
            _progress.Value = 0f;
            _loading.Value = true;
        }

        private void FinishLoading() {
            _progress.Value = 1f;
            _loading.Value = false;
            _progress.Value = 0f;
        }

        private void SetFatalError(string message, Exception exception) {
            _lastError.Value = message;
            _loading.Value = true;
            Debug.LogException(exception);
        }
    }
}
