
namespace Types {
     public enum StructureType {
          MayorOffice,
          Court,
          FireFighterStation,
          PoliceStation,
          Hospital,
          Archive,
     }

     public struct StructureInteraction {
          public StructureType Structure;
          public int InteractionResult;
     }
}