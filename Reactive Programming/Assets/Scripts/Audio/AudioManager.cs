using Bus;
using Types.Events;
using UnityEngine;

namespace Audio {
    public class AudioManager : MonoBehaviour {
        [SerializeField] private AudioSource _audioSource;
        
        private Listener<AudioCueRequestEvent> _audioCueRequestListener;

        private void Awake() {
            _audioCueRequestListener = new Listener<AudioCueRequestEvent>(OnRequest);
            EventBus<AudioCueRequestEvent>.Register(_audioCueRequestListener, this);
        }

        private void OnRequest(AudioCueRequestEvent e) {
            _audioSource.PlayOneShot(e.Cue.Clip);
        }
    }
}