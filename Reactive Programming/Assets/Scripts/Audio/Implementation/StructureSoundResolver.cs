using System;
using System.Linq;
using Bases.Objects;
using Components;
using Components.Instances;
using R3;
using Types;

namespace Audio.Implementation {
    public class StructureSoundResolver : IAudioDistributor, IDisposable {
        
        private CompositeDisposable _disposable { get; } = new();

        private StructureSoundConfig _config;
        private Subject<AudioCue> _cueRequest = new();

        public StructureSoundResolver(StructureSoundConfig config) {
            _config = config;
            
            var structureClick = ServiceLocator.Instance.GetService<StructureClickService>();
            
            structureClick.StructureInteraction.Select(interaction => interaction.Structure)
                .Subscribe(OnStructurePerformed).AddTo(_disposable);
        }        
        
        private void OnStructurePerformed(StructureType interaction) {
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