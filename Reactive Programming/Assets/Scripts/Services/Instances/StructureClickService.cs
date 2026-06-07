using Player;
using R3;
using Types;

namespace Components.Instances {
    public class StructureClickService : IService {
        
        private readonly Storage _storage;
        private readonly WorldCastService _worldCastService;
        
        private readonly Subject<StructureInteraction> _structureInteraction = new();
        public Observable<StructureInteraction> StructureInteraction => _structureInteraction;
        
        
        public StructureClickService(Storage storage, WorldCastService worldCastService) {
            _storage = storage;
            _worldCastService = worldCastService;
            _worldCastService.StructureClicked.Subscribe(HandleStructureInteraction);
        }
        
        public void HandleStructureInteraction(StructureType structure) {
            _storage.AddMoney(structure, 1);
            _structureInteraction.OnNext(new StructureInteraction { Structure = structure, InteractionResult = 1 });
        }
    }
}