namespace Data.Cache {
    
    public struct SignatureEntries {
        public string SignatureId { get; }
        
        public SignatureEntries(string signatureId) {
            SignatureId = signatureId;
        }
    }
}