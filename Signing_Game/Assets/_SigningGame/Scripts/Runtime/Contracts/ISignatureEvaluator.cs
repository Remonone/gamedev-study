using Data.Input;
using Data.Requests;
using Data.Results;

namespace Contracts {
    public interface ISignatureEvaluator {
        SignatureEvaluationResult Evaluate(SignatureEvaluationRequest request);
    }
}