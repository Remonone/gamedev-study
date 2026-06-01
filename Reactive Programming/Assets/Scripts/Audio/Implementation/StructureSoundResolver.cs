using System;
using System.Linq;
using Bases.Objects;
using Bus;
using Components;
using Components.Instances;
using R3;
using Types;
using Types.Events;

namespace Audio.Implementation {
    public class StructureSoundResolver : IService, IDisposable {
        
        private CompositeDisposable _disposable { get; } = new();

        private StructureSoundConfig _config;
        
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

            AudioCueRequestEvent e = new AudioCueRequestEvent(cue);
            EventBus<AudioCueRequestEvent>.Raise(e);
        }

        public void Dispose() {
            _disposable.Dispose();
        }
    }
}