using System;
using Services;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Presentation {
    public sealed class GameplaySettingsPopupView : MonoBehaviour {
        private GameObject _panelRoot;
        private Slider _musicSlider;
        private Slider _soundSlider;
        private Button _closeButton;
        private AudioSettingsViewModel _viewModel;
        private Action _close;
        private UnityAction<float> _musicAction;
        private UnityAction<float> _soundAction;
        private UnityAction _closeAction;

        public void Bind(AudioSettingsService settings, Action close) {
            Unbind();
            BuildUi();
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
            SetVisible(false);
        }

        public void SetVisible(bool visible) {
            if (_panelRoot != null) _panelRoot.SetActive(visible);
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

        private void BuildUi() {
            if (_panelRoot != null) return;

            Canvas parentCanvas = transform.parent != null
                ? transform.parent.GetComponent<Canvas>()
                : null;
            TMP_FontAsset font = parentCanvas != null
                ? GameplayUiFactory.ResolveFont(parentCanvas)
                : null;
            RectTransform root = transform as RectTransform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.localScale = Vector3.one;

            Image dim = GameplayUiFactory.CreateImage(
                "Settings Dim",
                root,
                new Color(0f, 0f, 0f, 0.55f),
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            dim.raycastTarget = true;

            Image panel = GameplayUiFactory.CreateImage(
                "Settings Panel",
                root,
                new Color(0.09f, 0.12f, 0.15f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(360f, 270f),
                Vector2.zero);
            _panelRoot = root.gameObject;

            GameplayUiFactory.CreateText(
                "Settings Title",
                panel.transform,
                font,
                "SETTINGS",
                24f,
                Color.white,
                TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(300f, 38f),
                new Vector2(0f, -30f));
            CreateSliderRow(panel.transform, font, "Music", -35f, out _musicSlider);
            CreateSliderRow(panel.transform, font, "Sound", -92f, out _soundSlider);
            _closeButton = GameplayUiFactory.CreateButton(
                "Settings Close",
                panel.transform,
                font,
                "Close",
                new Vector2(150f, 40f),
                new Vector2(0f, -135f),
                new Color(0.2f, 0.29f, 0.34f, 1f));
        }

        private static void CreateSliderRow(
            Transform parent,
            TMP_FontAsset font,
            string label,
            float y,
            out Slider slider) {
            GameplayUiFactory.CreateText(
                $"{label} Label",
                parent,
                font,
                label,
                16f,
                Color.white,
                TextAlignmentOptions.Left,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(70f, 30f),
                new Vector2(-120f, y));

            RectTransform sliderRoot = GameplayUiFactory.CreateRect(
                $"{label} Slider",
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(210f, 28f),
                new Vector2(40f, y));
            slider = sliderRoot.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.direction = Slider.Direction.LeftToRight;

            Image background = GameplayUiFactory.CreateImage(
                $"{label} Slider Background",
                sliderRoot,
                new Color(0.02f, 0.03f, 0.04f, 1f),
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            RectTransform fill = GameplayUiFactory.CreateRect(
                $"{label} Slider Fill",
                sliderRoot,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = new Color(0.35f, 0.72f, 0.9f, 1f);
            fillImage.raycastTarget = false;
            RectTransform handle = GameplayUiFactory.CreateRect(
                $"{label} Slider Handle",
                sliderRoot,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(22f, 36f),
                Vector2.zero);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = Color.white;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            background.raycastTarget = false;
        }

        private void Close() {
            _viewModel?.Flush();
            _close?.Invoke();
        }

        private void OnDestroy() => Unbind();
    }
}
