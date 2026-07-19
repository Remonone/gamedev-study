using Authoring;
using Data.Templates;

namespace Contracts {
    public interface ISignaturePresetRepository {
        CompiledSignaturePreset GetOrCompile(SignaturePresetDefinition preset);
        void Invalidate(SignaturePresetDefinition preset);
        void InvalidateById(string presetId);
        void Clear();
    }
}
