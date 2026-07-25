namespace Utils.Metadata {
    public readonly struct CacheParameterKey {
        public readonly string GroupId;
        public readonly string ParameterId;
        public CacheParameterKey(string groupId, string parameterId) {
            GroupId = groupId;
            ParameterId = parameterId;
        }
        
        public static CacheParameterKey Create(string groupId, string parameterId) => new CacheParameterKey(groupId, parameterId);
    }
}