using Data.Input;
using Data.Rules;
using Data.Templates;

namespace Data.Requests {
    public record SignatureEvaluationRequest(
        SignatureAttempt Attempt,
        CompiledSignaturePreset Preset,
        SignatureDifficultyRules Difficulty,
        SignatureRuleModifiers Modifiers
    );
}