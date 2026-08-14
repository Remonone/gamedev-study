using Authoring;
using Cysharp.Threading.Tasks;
using Data.Templates;
using System.Collections.Generic;

namespace Contracts {
    public interface ISignaturePresetRepository {
        IReadOnlyList<SignaturePresetDefinition> Presets { get; }
        CompiledSignaturePreset GetOrCompile(SignaturePresetDefinition preset);
        UniTask<SignaturePresetDefinition> RequestPreset(string id);
        bool TryGetPreset(string id, out SignaturePresetDefinition preset);
        void Invalidate(SignaturePresetDefinition preset);
        void InvalidateById(string presetId);
        void Clear();
    }
}
