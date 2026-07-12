using Authoring;
using Data.Rules;
using Data.Templates;

namespace Contracts {
    public interface ISignatureRulesResolver {
        ResolvedSignatureRules Resolve(CompiledSignaturePreset preset, SignatureDifficultyRules difficulty,
            SignatureProcessingRules processing, SignatureRuleModifiers modifiers);
    }
}