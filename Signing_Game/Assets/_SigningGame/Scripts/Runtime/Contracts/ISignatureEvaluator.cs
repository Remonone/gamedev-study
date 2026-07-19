using Authoring;
using Data.Input;
using Data.Requests;
using Data.Results;
using Data.Rules;

namespace Contracts {
    public interface ISignatureEvaluator {
        SignatureEvaluationResult Evaluate(SignatureEvaluationRequest request);
        SignatureEvaluationResult Evaluate(SignatureAttempt attempt, SignaturePresetDefinition preset,
            SignatureDifficultyRules difficulty, SignatureRuleModifiers modifiers);
    }
}
