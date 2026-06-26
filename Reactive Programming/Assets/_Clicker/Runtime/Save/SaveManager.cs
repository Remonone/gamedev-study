using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Utils;

namespace Save {
    public class SaveManager {
        private readonly SortedList<ISaveable> _saveables;
        private readonly string _path = Path.Combine(Application.persistentDataPath, "save.json");
        
        public SaveManager() {
            _saveables = new SortedList<ISaveable>(new SaveablesComparer());
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
                if (root.Payload.TryGetValue(saveable.SaveKey, out var data) && data is JObject reference) 
                    saveable.Load(reference);
            }
            
            Debug.Log("Load process finished.");
        }
        
        private class SaveablesComparer : IComparer<ISaveable> {

            public int Compare(ISaveable x, ISaveable y) {
                if (x == null) return -1;
                if (y == null) return 1;
                return y.Priority.CompareTo(x.Priority);
            } 
        }
    }
}