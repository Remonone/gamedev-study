using System;
using System.Collections.Generic;
using Authoring;
using Services;

namespace Presentation {
    public readonly struct StartingSignatureOption {
        public string Id { get; }
        public string DisplayName { get; }

        public StartingSignatureOption(string id, string displayName) {
            Id = id;
            DisplayName = displayName;
        }
    }

    public sealed class StartingSignatureSelectionViewModel : IDisposable {
        private readonly SignatureProgressionService _progression;
        private readonly List<StartingSignatureOption> _options = new(3);

        public IReadOnlyList<StartingSignatureOption> Options => _options;
        public bool IsSelectionRequired => _progression.RequiresStartingSelection;

        public StartingSignatureSelectionViewModel(SignatureProgressionService progression) {
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            for (int index = 0; progression.TryGetPendingPreset(index, out SignaturePresetDefinition preset); index++) {
                _options.Add(new StartingSignatureOption(preset.Id, preset.DisplayName));
            }
        }

        public bool Select(int index) {
            return index >= 0 && index < _options.Count &&
                   _progression.TrySelectStartingPreset(_options[index].Id);
        }

        public void Dispose() => _options.Clear();
    }
}
