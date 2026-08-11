using Data.Sound;
using UnityEngine;

namespace Services {
    public class AudioService: MonoBehaviour, IService {
        [SerializeField] private AudioSource _uiSource;
        [SerializeField] private AudioSource _musicSource;
        [SerializeField] private AudioSource _sfxSource;
        
        public void Dispose() {
            
        }
        
        public void PlayUI(AudioCue cue) {
            Play(_uiSource, cue);   
        }

        public void PlayMusic(AudioCue cue) {
            Play(_musicSource, cue);   
        }
        
        public void PlaySfx(AudioCue cue) {
            Play(_sfxSource, cue);   
        }

        private static void Play(AudioSource source, AudioCue cue) {
            if (source == null || cue == null || cue.Clips == null || cue.Clips.Length == 0) return;
            
            AudioClip clip = cue.Clips[Random.Range(0, cue.Clips.Length)];
            if (clip == null) return;
            source.pitch = GetPitch(cue.PitchRange);
            source.PlayOneShot(clip, cue.Volume);
        }

        private static float GetPitch(Vector2 range) {
            float minimum = Mathf.Min(range.x, range.y);
            float maximum = Mathf.Max(range.x, range.y);
            return Random.Range(minimum, maximum);
        }
    }
}
