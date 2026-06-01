using Audio;
using Bus;

namespace Types.Events {
    public class AudioCueRequestEvent : IEvent {
        public AudioCue Cue;
        
        public AudioCueRequestEvent(AudioCue cue) {
            Cue = cue;
        }
    }
}