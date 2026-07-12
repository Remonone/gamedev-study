using Authoring;
using Data.Templates;

namespace Contracts {
    public interface ISignaturePresetCompiler {
        CompiledSignaturePreset Compile(SignaturePresetDefinition preset);
    }
}