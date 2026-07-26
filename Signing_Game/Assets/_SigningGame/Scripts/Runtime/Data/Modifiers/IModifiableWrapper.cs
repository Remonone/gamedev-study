using System;
using System.Collections.Generic;
using Data.Modifiers.Calculation;
using Utils.Metadata;

namespace Data.Modifiers {
    public interface IModifiableWrapper {
        string GroupId { get; }
        string DisplayName { get; }
        Type EntryType { get; }
        IReadOnlyCollection<ICacheParameterMetadata> Parameters { get; }
        
        bool TryGetParameter(string parameterId, out ICacheParameterMetadata parameter);
        
        bool IsApplicable(object source);

        object Apply(object source, string parameterId, NumericModifierOperation operation, double operand);
    }
}