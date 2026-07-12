using Data.Input;
using Data.Processed;
using Data.Rules;

namespace Contracts {
    public interface ISignaturePreprocessor {
        ProcessedSignature Process(SignatureAttempt attempt, SignatureProcessingRules rules);
    }
}