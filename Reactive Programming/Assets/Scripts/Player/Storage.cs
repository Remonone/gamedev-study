using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using R3;
using Save;
using Types;
using UnityEngine;

namespace Player {
    public class Storage : IService, ISaveable {
        
        
        private Dictionary<StructureType, ReactiveProperty<long>> _structureMoney = new();

        public ReactiveProperty<long> this[StructureType type] {
            get => _structureMoney[type];
        }

        public Storage() {
            InitStructures();
        }

        private void InitStructures() {
            foreach (StructureType type in System.Enum.GetValues(typeof(StructureType))) {
                _structureMoney[type] = new ReactiveProperty<long>(0);
            }
        }

        public void AddMoney(StructureType type, long amount){
            _structureMoney[type].Value += amount;
        }

        public string SaveKey => "Storage";
        public int Priority => 99;
        
        public JToken Save() {
            var storageInfo = new JObject(
                new JProperty("Money", new JArray(
                            from structure in _structureMoney
                            select new JObject(
                                    new JProperty("type", structure.Key.ToString()),
                                    new JProperty("amount", structure.Value.Value)
                                )
                        )
                )
            );
            return storageInfo;
        }

        public void Load(JToken data) {
            if (data == null) return;
            foreach (var token in (JArray)data["Money"]) {
                var structureType = (StructureType)Enum.Parse(typeof(StructureType), (string)token["type"] ?? string.Empty);
                _structureMoney[structureType].Value = token["amount"]?.Value<long>() ?? 0;
            }
        }
    }
}