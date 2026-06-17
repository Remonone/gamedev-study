using System.Collections.Generic;

namespace Types.Enums {
    public interface ISessionContext {
        public int MayorInfluence { get; }
        public int FirefighterInfluence { get; }
        public int PoliceInfluence { get; }
        public int AmbulanceInfluence { get; }
        public int CourtInfluence { get; }
        public int ArchiveInfluence { get; }
        public int Seed { get; }
    }
}