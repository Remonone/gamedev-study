using Contracts;
using Data.Rules;
using Data.Templates;

namespace Services {
    public class RuleResolver : IService, ISignatureRulesResolver {
        public void Dispose() {
            
        }

        public ResolvedSignatureRules Resolve(CompiledSignaturePreset preset, SignatureDifficultyRules difficulty,
            SignatureProcessingRules processing, SignatureRuleModifiers modifiers) {
            throw new System.NotImplementedException();
        }
    }
}