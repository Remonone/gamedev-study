using Authoring;
using Cysharp.Threading.Tasks;
using Data.Templates;

namespace Contracts {
    public interface ISignaturePresetRepository {
        CompiledSignaturePreset GetOrCompile(SignaturePresetDefinition preset);
        UniTask<SignaturePresetDefinition> RequestPreset(string id);
        void Invalidate(SignaturePresetDefinition preset);
        void InvalidateById(string presetId);
        void Clear();
    }
}
