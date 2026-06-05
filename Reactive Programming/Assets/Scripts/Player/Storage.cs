using System.Collections.Generic;
using Newtonsoft.Json;
using R3;
using Save;
using Types;

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
        public string Save() {
            var data = new Dictionary<string, long>();
            foreach (var structure in _structureMoney) {
                data.Add(structure.Key.ToString(), structure.Value.CurrentValue);
            }
            return JsonConvert.SerializeObject(data);
        }

        public void Load(object data) {
            if (data == null) return;
            
            var json = JsonConvert.DeserializeObject<Dictionary<string, long>>((string)data);
            foreach (var structure in _structureMoney) {
                structure.Value.Value = json[structure.Key.ToString()];
            }
        }
    }
}