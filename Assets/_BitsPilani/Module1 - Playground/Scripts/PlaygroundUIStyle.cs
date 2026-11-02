using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BitsPilani.Module1.Playground {
    // Shared visuals for this module only. No prefab or package assets are edited.
    public static class PlaygroundUIStyle {
        public static readonly Color Ink = new Color(.035f, .095f, .13f);
        public static readonly Color Panel = new Color(.045f, .105f, .15f, .97f);
        public static readonly Color Card = new Color(.10f, .21f, .26f);
        public static readonly Color Mint = new Color(.53f, .94f, .80f);
        public static readonly Color Gold = new Color(1f, .80f, .40f);
        public static readonly Color Text = new Color(.94f, .98f, .98f);
        public static readonly Color Muted = new Color(.69f, .80f, .84f);
        static Sprite rounded;
        public static Sprite Rounded {
            get {
                if (rounded) return rounded;
                const int size = 96; const float radius = 20;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Module UI rounded corners", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) {
                    float dx = Mathf.Max(radius - (x + .5f), (x + .5f) - (size - radius));
                    float dy = Mathf.Max(radius - (y + .5f), (y + .5f) - (size - radius));
                    float distance = new Vector2(Mathf.Max(0, dx), Mathf.Max(0, dy)).magnitude;
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(radius - distance + .5f));
                }
                texture.SetPixels32(pixels); texture.Apply(false, true);
                rounded = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect, Vector4.one * 24);
                rounded.name = "Module rounded panel"; rounded.hideFlags = HideFlags.HideAndDontSave;
                return rounded;
            }
        }
        public static void Surface(Image image, Color color, bool border = false) {
            if (!image) return;
            image.sprite = Rounded; image.type = Image.Type.Sliced; image.color = color;
            if (border) {
                var edge = image.GetComponent<Outline>(); if (!edge) edge = image.gameObject.AddComponent<Outline>();
                edge.effectColor = new Color(.40f, .72f, .70f, .24f); edge.effectDistance = new Vector2(1, -1); edge.useGraphicAlpha = true;
            }
        }
        public static void Button(Button button, TMP_Text label, bool primary = false, bool selected = false) {
            if (!button) return;
            var image = button.targetGraphic as Image;
            Surface(image, Color.white);
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = selected ? new Color(.13f, .42f, .37f) : primary ? Mint : Card;
            colors.highlightedColor = Gold;
            colors.selectedColor = Gold;
            colors.pressedColor = new Color(.98f, .64f, .23f);
            colors.disabledColor = selected ? new Color(.13f, .42f, .37f) : new Color(.12f, .19f, .23f);
            colors.colorMultiplier = 1; colors.fadeDuration = .09f; button.colors = colors;
            var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
            var feedback = button.GetComponent<PlaygroundButtonFeedback>();
            if (!feedback) feedback = button.gameObject.AddComponent<PlaygroundButtonFeedback>();
            feedback.Configure(button, label, primary ? Ink : Text);
            if (label) { label.raycastTarget = false; label.enableAutoSizing = true; label.fontSizeMin = 20; label.fontSizeMax = 27; label.margin = new Vector4(7, 3, 7, 3); }
        }
        public static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string value, Vector2 size, Vector2 position, float fontSize) {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5; go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.sizeDelta = size; rect.anchoredPosition = position;
            var text = go.AddComponent<TextMeshProUGUI>(); text.font = font ? font : TMP_Settings.defaultFontAsset;
            text.text = value; text.fontSize = fontSize; text.color = Text; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            return text;
        }
    }
}
