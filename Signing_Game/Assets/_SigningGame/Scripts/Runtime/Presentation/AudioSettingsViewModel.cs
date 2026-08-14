using System;
using Services;

namespace Presentation {
    public sealed class AudioSettingsViewModel : IDisposable {
        private readonly AudioSettingsService _settings;

        public float MusicVolume => _settings.MusicVolume;
        public float SoundVolume => _settings.SoundVolume;

        public AudioSettingsViewModel(AudioSettingsService settings) {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void SetMusicVolume(float value) => _settings.SetMusicVolume(value);
        public void SetSoundVolume(float value) => _settings.SetSoundVolume(value);
        public void Flush() => _settings.Flush();
        public void Dispose() { }
    }
}
