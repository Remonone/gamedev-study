using Services.Player;
using R3;
using Services;
using Types;

namespace Components.Instances {
    public class StructureClickService : IService {
        
        private readonly Storage _storage;
        private readonly WorldCastService _worldCastService;
        private readonly UnlockService _unlockService;
        
        private readonly Subject<StructureInteraction> _structureInteraction = new();
        public Observable<StructureInteraction> StructureInteraction => _structureInteraction;
        
        
        public StructureClickService(Storage storage, WorldCastService worldCastService, UnlockService unlockService) {
            _storage = storage;
            _worldCastService = worldCastService;
            _worldCastService.StructureClicked.Subscribe(HandleStructureInteraction);
            _unlockService = unlockService;
        }
        
        private void HandleStructureInteraction(StructureType structure) {
            if (!_unlockService.IsItemUnlocked(structure.ToString())) return;
            _storage.AddMoney(structure, 1);
            _structureInteraction.OnNext(new StructureInteraction { Structure = structure, InteractionResult = 1 });
        }
    }
}