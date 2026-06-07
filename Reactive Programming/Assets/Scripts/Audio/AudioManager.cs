using Components;
using R3;
using UnityEngine;

namespace Audio {
    public class AudioManager : MonoBehaviour {
        [SerializeField] private AudioSource _audioSource;

        private void Start() {
            var distributors = ServiceLocator.Instance.GetServices<IAudioDistributor>();
            foreach (var distributor in distributors) {
                distributor.CueRequest.Subscribe(OnCueRequest).AddTo(this);
            }
        }

        private void OnCueRequest(AudioCue cue) {
            _audioSource.PlayOneShot(cue.Clip);
        }
    }
}