
using R3;

namespace Audio {
    public interface IAudioDistributor : IService {
        public Observable<AudioCue> CueRequest { get; }
    }
}