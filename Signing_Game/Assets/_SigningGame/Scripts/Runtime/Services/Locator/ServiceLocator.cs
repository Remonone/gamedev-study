using System;
using System.Collections.Generic;
using System.Linq;
using Bootstrap;
using Constants;
using Cysharp.Threading.Tasks;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Services.Locator {
    public class ServiceLocator : MonoBehaviour {
        private static ServiceLocator _applicationLocator;
        private static Dictionary<Scene, ServiceLocator> _sceneScopes = new();
        private static List<GameObject> _tmpSceneGameObjects = new();

        private ServiceScope _scope;

        public bool IsReady { get; private set; }

        public static ServiceLocator Application {
            get {
                if (_applicationLocator != null) return _applicationLocator;

                if (FindFirstObjectByType<GameGlobalBootstrapper>() is { } found) {
                    _ = found.BootstrapOnDemand();
                    return _applicationLocator;
                }

                var container = new GameObject(InternalConstants.GLOBAL_SERVICE_SCOPE, typeof(ServiceLocator));
                var locator = container.GetComponent<ServiceLocator>();
                locator._scope = new(locator);
                _ = container.AddComponent<GameGlobalBootstrapper>().BootstrapOnDemand();

                return _applicationLocator;
            }
        }

        public static ServiceLocator ForSceneOf(MonoBehaviour mb) {
            Scene scene = mb.gameObject.scene;
            ServiceLocator locator = ForScene(scene);
            if (locator != mb.GetComponent<ServiceLocator>()) return locator;

            return Application;
        }

        public static ServiceLocator ForScene(Scene scene) {
            if (_sceneScopes.TryGetValue(scene, out ServiceLocator locator)) {
                return locator;
            }

            _tmpSceneGameObjects.Clear();
            scene.GetRootGameObjects(_tmpSceneGameObjects);

            foreach (var go in _tmpSceneGameObjects.Where(go => go.GetComponent<SceneBootstrapper>() != null)) {
                if (go.TryGetComponent(out SceneBootstrapper bootstrapper)) {
                    _ = bootstrapper.BootstrapOnDemand();
                    return bootstrapper.Container;
                }
            }

            return Application;
        }

        public static ServiceLocator For(MonoBehaviour mb) {
            return mb.GetComponent<ServiceLocator>() ?? ForSceneOf(mb) ?? Application;
        }

        public ServiceLocator Register<T>(T service) where T : class {
            ServiceScope scope = _scope ?? new ServiceScope(this);
            scope.Register(service);
            _scope ??= scope;
            return this;
        }

        public ServiceLocator Register(Type type, object service) {
            ServiceScope scope = _scope ?? new ServiceScope(this);
            scope.Register(type, service);
            _scope ??= scope;
            return this;
        }

        public ServiceLocator Register(object service, params Type[] contracts) {
            ServiceScope scope = _scope ?? new ServiceScope(this);
            scope.Register(service, contracts);
            _scope ??= scope;
            return this;
        }

        public bool TryGetService<T>(out T service, int index = 0) where T : class {
            if (_scope != null) return _scope.TryGet(out service, index);
            Debug.LogWarning($"Trying to get service of type {typeof(T)} from a empty service locator with no scope.");
            service = null;
            return false;
        }

        public bool TryGet<T>(out T service, int index = 0) where T : class {
            var visited = new HashSet<ServiceLocator>(ServiceLocatorReferenceComparer.Instance);
            ServiceLocator current = this;
            while (current != null && visited.Add(current)) {
                if (current.TryGetService(out service, index)) return true;
                if (!current.TryGetNextInHierarchy(out current)) break;
            }

            service = null;
            return false;
        }

        public T Get<T>(int index = 0) where T : class {
            if (TryGet(out T service, index)) return service;
            throw new InvalidOperationException(
                $"Service contract '{typeof(T)}' has no registration at index {index} in the service locator hierarchy.");
        }

        public ServiceLocator Get<T>(out T service, int index = 0) where T : class {
            service = Get<T>(index);
            return this;
        }

        internal async UniTask InitializeScopeAsync() {
            await _scope.PreInitializeAsync(_scope);
            await _scope.InitializeAsync(_scope);
            await _scope.PostInitializeAsync(_scope);
            IsReady = true;
        }

        internal void ConfigureAsGlobal(bool dontDestroyOnLoad) {
            if (_applicationLocator == this) {
                Debug.LogWarning("ServiceLocator.ConfigureAsGlobal: Already configured as global", this);
            } else if (_applicationLocator != null) {
                Debug.LogError("ServiceLocator.ConfigureAsGlobal: Another ServiceLocator is already configured as global", this);
            } else {
                _applicationLocator = this;
                if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
            }
        }

        internal void ConfigureForScene() {
            Scene scene = gameObject.scene;

            if (_sceneScopes.ContainsKey(scene)) {
                Debug.LogError("ServiceLocator.ConfigureForScene: Another ServiceLocator is already configured for this scene", this);
                return;
            }
            
            _sceneScopes.Add(scene, this);
        }
        
        void OnDestroy() {
            _scope.Dispose();

            if (this == _applicationLocator) {
                _applicationLocator = null;
            } else if (_sceneScopes.ContainsValue(this)) {
                _sceneScopes.Remove(gameObject.scene);
            }
        }
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() {
            _applicationLocator = null;
            _sceneScopes = new Dictionary<Scene, ServiceLocator>();
            _tmpSceneGameObjects = new List<GameObject>();
        }

        public bool TryGetNextInHierarchy(out ServiceLocator container) {
            if (this == _applicationLocator) {
                container = null;
                return false;
            }
            
            container = transform.parent?.GetComponentInParent<ServiceLocator>() ?? ForSceneOf(this);
            return container != null;
        }

        private sealed class ServiceLocatorReferenceComparer : IEqualityComparer<ServiceLocator> {
            public static readonly ServiceLocatorReferenceComparer Instance = new();
            public bool Equals(ServiceLocator x, ServiceLocator y) => ReferenceEquals(x, y);
            public int GetHashCode(ServiceLocator obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
        
#if UNITY_EDITOR
        [MenuItem("GameObject/ServiceLocator/Add Global")]
        static void AddGlobal() {
            var go = new GameObject(InternalConstants.GLOBAL_SERVICE_SCOPE, typeof(GameGlobalBootstrapper));
        }

        [MenuItem("GameObject/ServiceLocator/Add Scene")]
        static void AddScene() {
            var go = new GameObject(InternalConstants.SCENE_PATTERN_SCOPE, typeof(SceneBootstrapper));
        }
#endif
    }
}
