using Components;
using Player;
using R3;
using Types;
using UnityEngine;

namespace Views.Models {
  public class Controls : MonoBehaviour {
        private Storage _storage;

        public ReactiveProperty<long> DocumentsCount = new ReactiveProperty<long>(0);
        public ReactiveProperty<long> CasesCount = new ReactiveProperty<long>(0);
        public ReactiveProperty<long> FiresCount = new ReactiveProperty<long>(0);
        public ReactiveProperty<long> ProtectionsCount = new ReactiveProperty<long>(0);
        public ReactiveProperty<long> CuresCount = new ReactiveProperty<long>(0);
  


        private void Start() {
            _storage = ServiceLocator.Instance.GetService<Storage>();

            _storage[StructureType.MayorOffice]
            .Subscribe(update => DocumentsCount.Value = update)
            .AddTo(this);
            
            _storage[StructureType.Court]
            .Subscribe(update => CasesCount.Value = update)
            .AddTo(this);

            _storage[StructureType.FireFighterStation]
            .Subscribe(update => FiresCount.Value = update)
            .AddTo(this);

            _storage[StructureType.PoliceStation]
            .Subscribe(update => ProtectionsCount.Value = update)
            .AddTo(this);

            _storage[StructureType.Hospital]
            .Subscribe(update => CuresCount.Value = update)
            .AddTo(this);
        }



        
    }
}