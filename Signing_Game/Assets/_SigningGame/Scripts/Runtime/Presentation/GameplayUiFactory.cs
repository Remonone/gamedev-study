using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation {
    internal static class GameplayUiFactory {
        public static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 size,
            Vector2 position) {
            var objectRoot = new GameObject(name, typeof(RectTransform));
            objectRoot.transform.SetParent(parent, false);
            var rect = (RectTransform)objectRoot.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one;
            return rect;
        }

        public static Image CreateImage(
            string name,
            Transform parent,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 size,
            Vector2 position) {
            RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax, pivot, size, position);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string text,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 size,
            Vector2 position) {
            RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax, pivot, size, position);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text ?? string.Empty;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            if (font != null) label.font = font;
            return label;
        }

        public static Button CreateButton(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string label,
            Vector2 size,
            Vector2 position,
            Color background = default) {
            if (background == default) background = new Color(0.16f, 0.2f, 0.24f, 0.96f);

            Image image = CreateImage(
                name,
                parent,
                background,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                size,
                position);
            Button button = image.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = background;
            colors.highlightedColor = Color.Lerp(background, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(background, Color.black, 0.18f);
            colors.disabledColor = new Color(background.r, background.g, background.b, 0.45f);
            button.colors = colors;
            CreateText(
                $"{name} Label",
                image.transform,
                font,
                label,
                16f,
                Color.white,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            return button;
        }

        public static TMP_FontAsset ResolveFont(Component source) {
            TMP_Text sample = source.GetComponentInChildren<TMP_Text>(true);
            return sample != null ? sample.font : null;
        }

        public static PullTabView CreatePullTab(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string label,
            Vector2 size,
            Vector2 handlePosition,
            Vector2 openPosition,
            Vector2 disabledPosition,
            Vector2 pulledPosition,
            Vector2 pulledSize,
            PullTabAxis axis,
            out RectTransform pulledObject) {
            Image handleImage = CreateImage(
                name,
                parent,
                new Color(0.12f, 0.16f, 0.2f, 0.96f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                size,
                handlePosition);
            handleImage.gameObject.AddComponent<CanvasGroup>();
            CreateText(
                $"{name} Label",
                handleImage.transform,
                font,
                label,
                14f,
                Color.white,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);

            RectTransform start = CreateMarker($"{name} Start", parent, handlePosition);
            RectTransform stop = CreateMarker($"{name} Stop", parent, openPosition);
            RectTransform disabled = CreateMarker($"{name} Disabled", parent, disabledPosition);
            Image pulledImage = CreateImage(
                $"{name} Content",
                parent,
                new Color(0.08f, 0.1f, 0.13f, 0.98f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                pulledSize,
                pulledPosition);
            pulledObject = (RectTransform)pulledImage.transform;
            pulledObject.gameObject.AddComponent<CanvasGroup>();

            PullTabView pullTab = handleImage.gameObject.AddComponent<PullTabView>();
            pullTab.Configure(axis, pulledObject, start, stop, disabled, threshold: 50f);
            return pullTab;
        }

        private static RectTransform CreateMarker(string name, Transform parent, Vector2 position) {
            return CreateRect(
                name,
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                position);
        }
    }
}
