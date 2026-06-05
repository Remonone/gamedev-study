using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Save {
    public class SaveManager {
        private readonly List<ISaveable> _saveables;
        private readonly string _path = Path.Combine(Application.persistentDataPath, "save.json");
        
        public SaveManager() {
            _saveables = new List<ISaveable>();
        }
        
        public void Register(ISaveable saveable) => _saveables.Add(saveable);

        public void Save() {
            Debug.Log("Starting save process...");
            var root = new SaveData {
                SavedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            foreach (var saveable in _saveables) {
                root.Payload[saveable.SaveKey] = saveable.Save();
            }
            
            var json = JsonConvert.SerializeObject(root);
            File.WriteAllText(_path, json);
            Debug.Log("Save process finished.");
        }

        public void Load() {
            if (!File.Exists(_path)) return;
            Debug.Log("Starting load process...");
            
            var json = File.ReadAllText(_path);
            var root = JsonConvert.DeserializeObject<SaveData>(json);
            
            foreach(var saveable in _saveables) {
                if (root.Payload.TryGetValue(saveable.SaveKey, out var data)) 
                    saveable.Load(data);
            }
            
            Debug.Log("Load process finished.");
        }
    }
}