using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BitsPilani.Module1.Playground {
    // Shared visuals for this module only. No prefab or package assets are edited.
    public static class PlaygroundUIStyle {
        public static readonly Color Ink = new Color(.055f, .055f, .055f);
        public static readonly Color Panel = new Color(.955f, .955f, .955f);
        public static readonly Color Card = Color.white;
        public static readonly Color Inset = new Color(.90f, .90f, .90f);
        public static readonly Color Disabled = new Color(.92f, .92f, .92f);
        // Legacy accent names map to the monochrome palette for existing scene scripts.
        public static readonly Color Mint = new Color(.28f, .28f, .28f);
        public static readonly Color Gold = new Color(.055f, .055f, .055f);
        public static readonly Color Text = new Color(.16f, .16f, .16f);
        public static readonly Color Muted = new Color(.37f, .37f, .37f);
        static Sprite rounded;
        public static void UseRoundedAsset(Sprite value) { rounded = value; }
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
                edge.effectColor = new Color(0, 0, 0, .10f); edge.effectDistance = new Vector2(1, -1); edge.useGraphicAlpha = true;
            }
        }
        public static void CardShadow(Image image, float opacity = .11f, float offset = 3f) {
            if (!image) return;
            // Outline derives from Shadow, so find an actual drop-shadow component.
            Shadow shadow = null;
            foreach (var candidate in image.GetComponents<Shadow>()) if (candidate.GetType() == typeof(Shadow)) { shadow = candidate; break; }
            if (!shadow) shadow = image.gameObject.AddComponent<Shadow>();
            shadow.enabled = true; shadow.effectColor = new Color(0, 0, 0, opacity);
            shadow.effectDistance = new Vector2(0, -offset); shadow.useGraphicAlpha = true;
        }
        public static void LightPanel(GameObject panel) {
            if (!panel) return;
            foreach (var image in panel.GetComponentsInChildren<Image>(true)) if (image.color.a > .01f) Surface(image, Color.white);
            foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true)) text.color = Color.black;
            foreach (var text in panel.GetComponentsInChildren<UnityEngine.UI.Text>(true)) text.color = Color.black;
            foreach (var field in panel.GetComponentsInChildren<TMP_InputField>(true)) {
                field.customCaretColor = true; field.caretColor = Color.black;
                field.selectionColor = new Color(.7f, .7f, .7f, .5f);
                field.transition = Selectable.Transition.None;
                if (field.targetGraphic) field.targetGraphic.CrossFadeColor(Color.white, 0, true, true);
            }
            foreach (var button in panel.GetComponentsInChildren<Button>(true)) Button(button, button.GetComponentInChildren<TMP_Text>(true));
        }
        public static void Button(Button button, TMP_Text label, bool primary = false, bool selected = false) {
            if (!button) return;
            var image = button.targetGraphic as Image;
            Color baseFill = selected ? new Color(.89f, .89f, .89f) : Color.white;
            Surface(image, baseFill, true);
            button.transition = Selectable.Transition.None;
            var colors = button.colors;
            colors.normalColor = colors.highlightedColor = colors.selectedColor = colors.pressedColor = colors.disabledColor = Color.white;
            colors.colorMultiplier = 1; colors.fadeDuration = 0; button.colors = colors;
            var edge = image ? image.GetComponent<Outline>() : null;
            if (edge) { edge.enabled = true; edge.effectColor = selected || primary ? new Color(0, 0, 0, .38f) : new Color(0, 0, 0, .16f); edge.effectDistance = new Vector2(.7f, -.7f); }
            CardShadow(image, button.IsInteractable() ? .11f : .04f, 3f);
            var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
            var feedback = button.GetComponent<PlaygroundButtonFeedback>();
            if (!feedback) feedback = button.gameObject.AddComponent<PlaygroundButtonFeedback>();
            feedback.Configure(button, label, Ink, baseFill);
            if (label) { label.fontStyle = primary || selected ? FontStyles.Bold : FontStyles.Normal; label.raycastTarget = false; label.enableAutoSizing = true; label.fontSizeMin = 20; label.fontSizeMax = 27; label.margin = new Vector4(7, 3, 7, 3); }
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
