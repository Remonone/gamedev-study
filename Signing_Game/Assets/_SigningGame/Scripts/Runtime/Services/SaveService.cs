using System;
using System.Collections.Generic;
using System.IO;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Services.Locator;
using UnityEngine;

namespace Services {
    public sealed class SaveService : IService, IPreInitialize {
        private const string DefaultFileName = "save.json";

        private readonly string _filePath;
        private readonly List<ISaveable> _saveables = new();
        private bool _saveablesDiscovered;

        public string FilePath => _filePath;

        public SaveService() : this(Path.Combine(Application.persistentDataPath, DefaultFileName)) { }

        public SaveService(string filePath) {
            if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("Save file path cannot be empty.", nameof(filePath));
            }

            _filePath = filePath;
        }

        public UniTask PreInitializeAsync(IServiceScope scope) {
            DiscoverSaveables(scope);
            TryLoadFromFile();
            return UniTask.CompletedTask;
        }

        public SaveSnapshot CreateSnapshot() {
            EnsureSaveablesDiscovered();
            var sections = new Dictionary<string, JToken>(_saveables.Count, StringComparer.Ordinal);

            foreach (ISaveable saveable in _saveables) {
                JToken state = saveable.Serialize();
                if (state == null) {
                    throw new InvalidOperationException($"Saveable service '{saveable.SaveId}' returned a null state.");
                }

                sections.Add(saveable.SaveId, state);
            }

            return new SaveSnapshot(SaveSnapshot.CurrentVersion, sections);
        }

        public bool LoadSnapshot(SaveSnapshot snapshot) {
            EnsureSaveablesDiscovered();
            if (snapshot == null) {
                Debug.LogWarning("Cannot load a null save snapshot.");
                return false;
            }

            if (snapshot.Version != SaveSnapshot.CurrentVersion) {
                Debug.LogWarning(
                    $"Unsupported save version '{snapshot.Version}'. Expected version '{SaveSnapshot.CurrentVersion}'.");
                return false;
            }

            if (snapshot.Sections == null) {
                Debug.LogWarning("Save snapshot has no sections map.");
                return false;
            }

            bool loadedAllSections = true;
            foreach (ISaveable saveable in _saveables) {
                if (!snapshot.Sections.TryGetValue(saveable.SaveId, out JToken state) || state == null) continue;

                try {
                    saveable.Deserialize(state);
                } catch (Exception exception) {
                    loadedAllSections = false;
                    Debug.LogWarning(
                        $"Failed to restore save section '{saveable.SaveId}'. The service kept its current state.\n{exception}");
                }
            }

            return loadedAllSections;
        }

        public bool SaveToFile() {
            string temporaryPath = _filePath + ".tmp";

            try {
                SaveSnapshot snapshot = CreateSnapshot();
                string json = JsonConvert.SerializeObject(snapshot, Formatting.Indented);
                string directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                File.WriteAllText(temporaryPath, json);

                if (File.Exists(_filePath)) {
                    File.Replace(temporaryPath, _filePath, null);
                } else {
                    File.Move(temporaryPath, _filePath);
                }

                return true;
            } catch (Exception exception) {
                Debug.LogWarning($"Failed to save game to '{_filePath}'.\n{exception}");
                return false;
            } finally {
                try {
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                } catch (Exception exception) {
                    Debug.LogWarning($"Failed to clean temporary save file '{temporaryPath}'.\n{exception}");
                }
            }
        }

        public bool TryLoadFromFile() {
            if (!File.Exists(_filePath)) return false;

            try {
                string json = File.ReadAllText(_filePath);
                if (!TryDeserializeSnapshot(json, out SaveSnapshot snapshot)) return false;
                return LoadSnapshot(snapshot);
            } catch (Exception exception) {
                Debug.LogWarning($"Failed to load game from '{_filePath}'. The default state will be used.\n{exception}");
                return false;
            }
        }

        public void Dispose() { }

        private void DiscoverSaveables(IServiceScope scope) {
            if (_saveablesDiscovered) return;

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; scope.TryGet(out ISaveable saveable, index); index++) {
                if (string.IsNullOrWhiteSpace(saveable.SaveId)) {
                    throw new InvalidOperationException(
                        $"Saveable service '{saveable.GetType().Name}' must have a non-empty save ID.");
                }

                if (!ids.Add(saveable.SaveId)) {
                    throw new InvalidOperationException($"Duplicate save ID '{saveable.SaveId}' was registered.");
                }

                _saveables.Add(saveable);
            }

            _saveablesDiscovered = true;
        }

        private bool TryDeserializeSnapshot(string json, out SaveSnapshot snapshot) {
            snapshot = null;
            JObject root;

            try {
                root = JObject.Parse(json);
            } catch (JsonException exception) {
                Debug.LogWarning($"Save file '{_filePath}' contains invalid JSON.\n{exception}");
                return false;
            }

            JToken versionToken = root["version"];
            if (versionToken?.Type != JTokenType.Integer) {
                Debug.LogWarning($"Save file '{_filePath}' has no valid schema version.");
                return false;
            }

            int version = versionToken.Value<int>();
            if (version != SaveSnapshot.CurrentVersion) {
                Debug.LogWarning(
                    $"Unsupported save version '{version}' in '{_filePath}'. Expected '{SaveSnapshot.CurrentVersion}'.");
                return false;
            }

            if (root["sections"] is not JObject sectionsObject) {
                Debug.LogWarning($"Save file '{_filePath}' has no valid sections object.");
                return false;
            }

            var sections = new Dictionary<string, JToken>(StringComparer.Ordinal);
            foreach (JProperty property in sectionsObject.Properties()) {
                sections.Add(property.Name, property.Value.DeepClone());
            }

            snapshot = new SaveSnapshot(version, sections);
            return true;
        }

        private void EnsureSaveablesDiscovered() {
            if (!_saveablesDiscovered) {
                throw new InvalidOperationException(
                    "Saveable services are not available until SaveService.PreInitializeAsync has run.");
            }
        }
    }
}
