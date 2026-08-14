using Data.Sound;
using UnityEngine;

namespace Services {
    public sealed class AudioService : MonoBehaviour, IService {
        [SerializeField] private AudioSource _uiSource;
        [SerializeField] private AudioSource _musicSource;
        [SerializeField] private AudioSource _sfxSource;
        
        public float MusicVolume { get; private set; } = 1f;
        public float SoundVolume { get; private set; } = 1f;

        public void Dispose() { }

        public void SetMusicVolume(float volume) {
            MusicVolume = Mathf.Clamp01(volume);
            if (_musicSource != null) _musicSource.volume = MusicVolume;
        }

        public void SetSoundVolume(float volume) {
            SoundVolume = Mathf.Clamp01(volume);
            if (_uiSource != null) _uiSource.volume = SoundVolume;
            if (_sfxSource != null) _sfxSource.volume = SoundVolume;
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
