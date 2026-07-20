using Authoring;
using Data.Input;
using Data.Rules;

namespace Data.Requests {
    public record SignatureEvaluationRequest(
        SignatureAttempt Attempt,
        SignaturePresetDefinition Preset,
        SignatureDifficultyRules Difficulty,
        SignatureRuleModifiers Modifiers
    );
}