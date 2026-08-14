using System;
using UnityEngine;

namespace Services {
    public sealed class AudioSettingsService : IService {
        private const string MusicVolumeKey = "settings.musicVolume";
        private const string SoundVolumeKey = "settings.soundVolume";

        private readonly AudioService _audio;
        private bool _dirty;

        public float MusicVolume { get; private set; }
        public float SoundVolume { get; private set; }

        public AudioSettingsService(AudioService audio) {
            _audio = audio ?? throw new ArgumentNullException(nameof(audio));
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, 1f));
            SoundVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SoundVolumeKey, 1f));
            _audio.SetMusicVolume(MusicVolume);
            _audio.SetSoundVolume(SoundVolume);
        }

        public void SetMusicVolume(float value) {
            MusicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
            _audio.SetMusicVolume(MusicVolume);
            _dirty = true;
        }

        public void SetSoundVolume(float value) {
            SoundVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SoundVolumeKey, SoundVolume);
            _audio.SetSoundVolume(SoundVolume);
            _dirty = true;
        }

        public void Flush() {
            if (!_dirty) return;
            PlayerPrefs.Save();
            _dirty = false;
        }

        public void Dispose() => Flush();
    }
}
