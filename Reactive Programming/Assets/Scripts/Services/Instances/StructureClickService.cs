using R3;
using Types;

namespace Components.Instances {
    public class StructureClickService : IService {
        
        public Subject<StructureInteraction> StructureInteraction { get; } = new();
        
        public StructureClickService() {
            
        }
        
        public void HandleStructureInteraction(StructureType structure) {
            StructureInteraction.OnNext(new StructureInteraction { Structure = structure, InteractionResult = 1 });
        }
    }
}