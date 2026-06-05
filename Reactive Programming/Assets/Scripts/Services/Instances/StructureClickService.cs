using Player;
using R3;
using Types;

namespace Components.Instances {
    public class StructureClickService : IService {
        
        private readonly Storage _storage;
        
        private readonly Subject<StructureInteraction> _structureInteraction = new();
        public Observable<StructureInteraction> StructureInteraction => _structureInteraction;
        
        
        public StructureClickService(Storage storage) {
            _storage = storage;
        }
        
        public void HandleStructureInteraction(StructureType structure) {
            _storage.AddMoney(structure, 1);
            _structureInteraction.OnNext(new StructureInteraction { Structure = structure, InteractionResult = 1 });
        }
    }
}