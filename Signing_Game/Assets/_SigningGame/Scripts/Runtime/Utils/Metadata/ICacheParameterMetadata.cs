using System;

namespace Utils.Metadata {
    public interface ICacheParameterMetadata {
        CacheParameterKey Key { get; }
        string DisplayName { get; }
        Type EntryType { get; }
        Type ValueType { get; }
        double Minimum { get; }
        double Maximum { get; }
        object GetValue(object boxedEntry);
        
        void SetValue(object boxedEntry, object value);
    }
}