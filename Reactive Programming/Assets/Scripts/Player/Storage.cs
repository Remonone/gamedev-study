using System;
using System.Collections.Generic;
using Components;
using Components.Instances;
using R3;
using Types;

namespace Player {
    public class Storage : IService, IDisposable {
        
        private CompositeDisposable _disposable { get; } = new();
        
        private Dictionary<StructureType, ReactiveProperty<long>> _structureMoney = new();

        public ReactiveProperty<long> this[StructureType type] {
            get => _structureMoney[type];
        }

        public Storage() {
            InitStructures();
            var structureClick = ServiceLocator.Instance.GetService<StructureClickService>();
            structureClick.StructureInteraction.Subscribe(interaction => AddMoney(interaction.Structure, interaction.InteractionResult)).AddTo(_disposable);
        }

        private void InitStructures() {
            foreach (StructureType type in System.Enum.GetValues(typeof(StructureType))) {
                _structureMoney[type] = new ReactiveProperty<long>(0);
            }
        }

        public void AddMoney(StructureType type, long amount){
            _structureMoney[type].Value += amount;
        }

        public void Dispose() {
            _disposable.Dispose();
        }
    }
}