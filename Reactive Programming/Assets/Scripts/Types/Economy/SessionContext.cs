namespace Types.Enums {
    public class SessionContext : ISessionContext {
        
        private int _mayorInfluence;
        private int _firefighterInfluence;
        private int _policeInfluence;
        private int _ambulanceInfluence;
        private int _courtInfluence;
        private int _archiveInfluence;
        private int _seed;
        
        public int MayorInfluence => _mayorInfluence;
        public int FirefighterInfluence => _firefighterInfluence;
        public int PoliceInfluence => _policeInfluence;
        public int AmbulanceInfluence => _ambulanceInfluence;
        public int CourtInfluence => _courtInfluence;
        public int ArchiveInfluence => _archiveInfluence;
        public int Seed => _seed;

        public SessionContext() {
        }
        
        public SessionContext(int mayorInfluence, int firefighterInfluence, int policeInfluence, int ambulanceInfluence, int courtInfluence, int archiveInfluence) {
            _mayorInfluence = mayorInfluence;
            _firefighterInfluence = firefighterInfluence;
            _policeInfluence = policeInfluence;
            _ambulanceInfluence = ambulanceInfluence;
            _courtInfluence = courtInfluence;
            _archiveInfluence = archiveInfluence;
            _seed = UnityEngine.Random.Range(0, int.MaxValue);
        }
        
        public override string ToString() {
            return $"Mayor: {_mayorInfluence}, Firefighter: {_firefighterInfluence}, Police: {_policeInfluence}, Ambulance: {_ambulanceInfluence}, Court: {_courtInfluence}, Archive: {_archiveInfluence}";
        }
        
        public void SetMayorInfluence(int influence) {
            _mayorInfluence = influence;
        }
        
        public void SetFirefighterInfluence(int influence) {
            _firefighterInfluence = influence;
        }
        
        public void SetPoliceInfluence(int influence) {
            _policeInfluence = influence;
        }
        
        public void SetAmbulanceInfluence(int influence) {
            _ambulanceInfluence = influence;
        }
        
        public void SetCourtInfluence(int influence) {
            _courtInfluence = influence;
        }
        
        public void SetArchiveInfluence(int influence) {
            _archiveInfluence = influence;
        }
    }
}