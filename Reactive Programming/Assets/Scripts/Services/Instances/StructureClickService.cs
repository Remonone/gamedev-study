using Player;
using R3;
using Types;

namespace Components.Instances {
    public class StructureClickService : IService {
        
        private readonly Storage _storage;
        
        public Subject<StructureInteraction> StructureInteraction { get; } = new();
        
        
        public StructureClickService(Storage storage) {
            _storage = storage;
        }
        
        public void HandleStructureInteraction(StructureType structure) {
            _storage.AddMoney(structure, 1);
            StructureInteraction.OnNext(new StructureInteraction { Structure = structure, InteractionResult = 1 });
        }
    }
}