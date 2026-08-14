using System;
using Services;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Presentation {
    public sealed class AudioSettingsView : MonoBehaviour {
        [SerializeField] private Slider _musicSlider;
        [SerializeField] private Slider _soundSlider;
        [SerializeField] private Button _closeButton;

        private AudioSettingsViewModel _viewModel;
        private Action _close;
        private UnityAction<float> _musicAction;
        private UnityAction<float> _soundAction;
        private UnityAction _closeAction;

        public void Bind(AudioSettingsService settings, Action close) {
            Unbind();
            _viewModel = new AudioSettingsViewModel(settings);
            _close = close;
            _musicSlider.SetValueWithoutNotify(_viewModel.MusicVolume);
            _soundSlider.SetValueWithoutNotify(_viewModel.SoundVolume);
            _musicAction = _viewModel.SetMusicVolume;
            _soundAction = _viewModel.SetSoundVolume;
            _closeAction = Close;
            _musicSlider.onValueChanged.AddListener(_musicAction);
            _soundSlider.onValueChanged.AddListener(_soundAction);
            _closeButton.onClick.AddListener(_closeAction);
        }

        public void Unbind() {
            if (_musicAction != null) _musicSlider?.onValueChanged.RemoveListener(_musicAction);
            if (_soundAction != null) _soundSlider?.onValueChanged.RemoveListener(_soundAction);
            if (_closeAction != null) _closeButton?.onClick.RemoveListener(_closeAction);
            _viewModel?.Dispose();
            _viewModel = null;
            _close = null;
            _musicAction = null;
            _soundAction = null;
            _closeAction = null;
        }

        private void Close() {
            _viewModel?.Flush();
            _close?.Invoke();
        }

        private void OnDestroy() => Unbind();
    }
}
