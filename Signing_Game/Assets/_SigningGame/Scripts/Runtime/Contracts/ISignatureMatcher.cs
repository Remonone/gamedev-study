using Data.Processed;
using Data.Results;
using Data.Rules;
using Data.Templates;

namespace Contracts {
    public interface ISignatureMatcher {
        SignatureVariantMatchResult Match(ProcessedSignature input, SignatureTemplateVariant template,
            ResolvedSignatureRules rules);
    }
}