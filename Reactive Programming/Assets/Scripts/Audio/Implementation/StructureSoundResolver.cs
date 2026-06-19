using System;
using System.Linq;
using Types.Enums.Objects;
using Services.Components;
using Services.Components.Instances;
using R3;
using Types.Enums;

namespace Audio.Implementation {
    public class StructureSoundResolver : IAudioDistributor, IDisposable {
        
        private CompositeDisposable _disposable { get; } = new();

        private StructureSoundConfig _config;
        private Subject<AudioCue> _cueRequest = new();

        public StructureSoundResolver(StructureClickService clickService, StructureSoundConfig config) {
            _config = config;
            
            var structureClick = clickService;
            
            structureClick.StructureInteraction.Select(interaction => interaction.GovernmentInteraction)
                .Subscribe(OnStructurePerformed).AddTo(_disposable);
        }        
        
        private void OnStructurePerformed(GovernmentInteractionType interaction) {
            var cue = _config.Clips.Where(clip => clip.Type.Equals(interaction))
                .Where(clip => clip.Cue.Clip != null)
                .Select(clip => clip.Cue).FirstOrDefault();
            if (cue.Clip == null) {
                return;
            }
            _cueRequest.OnNext(cue);
        }

        public void Dispose() {
            _disposable.Dispose();
        }

        public Observable<AudioCue> CueRequest => _cueRequest;
    }
}