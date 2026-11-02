using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BitsPilani.Module1.Playground {
    // BNG sends standard pointer events. Use them for an unmistakable hover border and label.
    public sealed class PlaygroundButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler {
        Button button;
        TMP_Text label;
        Outline border;
        Color idleText;
        bool hovered, selected, pressed;
        public Action<bool> HoverChanged;
        public bool IsHovered => hovered;
        public void Configure(Button target, TMP_Text text, Color normalText) {
            button = target; label = text; idleText = normalText;
            border = GetComponent<Outline>(); if (!border) border = gameObject.AddComponent<Outline>();
            border.effectColor = PlaygroundUIStyle.Gold; border.effectDistance = new Vector2(3, -3); border.useGraphicAlpha = false;
            Paint();
        }
        public void OnPointerEnter(PointerEventData data) { hovered = true; Paint(); HoverChanged?.Invoke(button && button.interactable); }
        public void OnPointerExit(PointerEventData data) { hovered = selected = pressed = false; Paint(); HoverChanged?.Invoke(false); }
        public void OnPointerDown(PointerEventData data) { pressed = true; Paint(); }
        public void OnPointerUp(PointerEventData data) { pressed = false; Paint(); }
        public void OnSelect(BaseEventData data) { selected = true; Paint(); }
        public void OnDeselect(BaseEventData data) { selected = false; Paint(); }
        void Paint() {
            bool focused = button && button.interactable && (hovered || selected || pressed);
            if (border) border.enabled = focused;
            if (label) label.color = focused ? PlaygroundUIStyle.Ink : idleText;
            if (button && button.targetGraphic) {
                var colors = button.colors;
                Color fill = !button.interactable ? colors.disabledColor : focused ? (pressed ? colors.pressedColor : colors.highlightedColor) : colors.normalColor;
                // Immediate contrast matters for a moving controller ray; do not wait for a tint tween.
                button.targetGraphic.CrossFadeColor(fill, 0, true, true);
            }
        }
        void OnDisable() { hovered = selected = pressed = false; Paint(); HoverChanged?.Invoke(false); }
    }
}
