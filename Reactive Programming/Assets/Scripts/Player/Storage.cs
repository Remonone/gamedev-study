using System.Collections.Generic;
using R3;
using Types;

namespace Player {
    public class Storage : IService {
        
        
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

    }
}