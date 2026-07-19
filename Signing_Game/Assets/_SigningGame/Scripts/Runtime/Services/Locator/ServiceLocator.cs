using System;
using System.Collections.Generic;
using System.Linq;
using Bootstrap;
using Constants;
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

        private ServiceScope _scope = new();

        public bool IsReady { get; private set; }

        public static ServiceLocator Application {
            get {
                if (_applicationLocator != null) return _applicationLocator;

                if (FindFirstObjectByType<GameGlobalBootstrapper>() is { } found) {
                    _ = found.BootstrapOnDemand();
                    return _applicationLocator;
                }

                var container = new GameObject(InternalConstants.GLOBAL_SERVICE_SCOPE, typeof(ServiceLocator));
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

        public ServiceLocator Register<T>(T service) where T : IService {
            _scope.Register(service);
            return this;
        }

        public ServiceLocator Register(Type type, object service) {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (!type.IsInstanceOfType(service))
                throw new ArgumentException($"Service is not assignable to {type}.", nameof(service));
            if (service is not IService)
                throw new ArgumentException("Service instance must implement IService.", nameof(service));
            _scope.Register(type, service);
            return this;
        }


        public bool TryGetService<T>(out T service) where T : class {
            return _scope.TryGet(out service);
        }

        public bool TryGet<T>(out T service) where T : class {
            var visited = new HashSet<ServiceLocator>(ServiceLocatorReferenceComparer.Instance);
            ServiceLocator current = this;
            while (current != null && visited.Add(current)) {
                if (current.TryGetService(out service)) return true;
                if (!current.TryGetNextInHierarchy(out current)) break;
            }

            service = null;
            return false;
        }

        public T Get<T>() where T : class {
            if (TryGet(out T service)) return service;
            throw new InvalidOperationException(
                $"Service of type {typeof(T)} was not found in the service locator hierarchy.");
        }

        public ServiceLocator Get<T>(out T service) where T : class {
            service = Get<T>();
            return this;
        }

        internal async Awaitable InitializeScopeAsync() {
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
