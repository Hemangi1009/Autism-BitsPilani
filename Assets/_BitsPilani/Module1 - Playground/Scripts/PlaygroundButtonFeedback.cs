using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BitsPilani.Module1.Playground {
    // Monochrome hover inversion using the existing BNG/controller pointer events.
    public sealed class PlaygroundButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler {
        [Range(1f, 1.12f)] public float hoverScale = 1.025f;
        [Range(.04f, .4f)] public float hoverSmoothTime = .10f;
        [Header("Inspector-authored interaction colors")]
        public Color hoverBackground = Color.black;
        public Color hoverText = Color.white;
        public Color disabledBackground = new Color(.92f,.92f,.92f);
        public Color disabledText = new Color(.37f,.37f,.37f);
        public Color selectedBackground = new Color(.88f,.88f,.88f);
        public Color selectedText = Color.black;
        public Color hoverBorder = Color.black;
        public Color selectedBorder = Color.black;
        Button button;
        TMP_Text label;
        readonly Dictionary<TMP_Text, Color> idleLabels = new Dictionary<TMP_Text, Color>();
        Color idleFill = Color.white, idleBorder;
        Outline border;
        bool lastCanInteract, choiceSelected;
        Vector3 authoredScale;
        bool hasAuthoredScale, hovered, selected;
        float factor = 1f, velocity;
        public Action<bool> HoverChanged;
        public bool IsHovered => hovered;
        public void Configure(Button target, TMP_Text text, Color normalText, Color? normalFill = null) {
            button = target; label = text;
            idleFill = normalFill ?? Color.white;
            if (label) idleLabels[label] = normalText;
            border = GetComponent<Outline>(); if (border) idleBorder = border.effectColor;
            // Repeated UI updates must not capture an enlarged scale as the baseline.
            if (!hasAuthoredScale) { authoredScale = transform.localScale; hasAuthoredScale = true; }
            Paint();
        }
        void Awake() { CaptureAuthoredAppearance(); }
        void CaptureAuthoredAppearance() {
            if (hasAuthoredScale) return;
            button = GetComponent<Button>();
            border = GetComponent<Outline>(); if (border) idleBorder = border.effectColor;
            if (button && button.targetGraphic) idleFill = button.targetGraphic.color;
            foreach (var text in GetComponentsInChildren<TMP_Text>(true)) idleLabels[text] = text.color;
            authoredScale = transform.localScale; hasAuthoredScale = true;
        }
        // Restore the normal appearance before the Editor saves a prepared canvas.
        public void RestoreAuthoredAppearance() {
            if (button && button.targetGraphic) button.targetGraphic.color = idleFill;
            if (border) border.effectColor = idleBorder;
            foreach (var pair in idleLabels) if (pair.Key) pair.Key.color = pair.Value;
            ResetScale();
        }
        public void SetChoiceSelected(bool value) { CaptureAuthoredAppearance(); choiceSelected = value; Paint(); }
        bool CanInteract => button && button.IsActive() && button.IsInteractable();
        public void OnPointerEnter(PointerEventData data) { hovered = true; Paint(); HoverChanged?.Invoke(CanInteract); }
        public void OnPointerExit(PointerEventData data) { hovered = selected = false; Paint(); HoverChanged?.Invoke(false); }
        public void OnPointerDown(PointerEventData data) { Paint(); }
        public void OnPointerUp(PointerEventData data) { Paint(); }
        public void OnSelect(BaseEventData data) { selected = true; Paint(); }
        public void OnDeselect(BaseEventData data) { selected = false; Paint(); }
        void Paint() {
            bool interactive = CanInteract;
            bool focused = interactive && (hovered || selected);
            // Module cards have multiple labels. Invert every label inside that card,
            // then restore each label's own gray/black shade when the pointer leaves.
            foreach (var text in GetComponentsInChildren<TMP_Text>(true)) {
                if (!idleLabels.ContainsKey(text)) idleLabels[text] = text.color;
                // text.color = focused ? hoverText : !interactive ? disabledText : idleLabels[text];
                text.color = !interactive ? disabledText
                    : choiceSelected ? selectedText
                    : focused ? hoverText
                    : idleLabels[text];
            }
            if (border) border.effectColor = focused ? hoverBorder : choiceSelected ? selectedBorder : idleBorder;
            if (button && button.targetGraphic) {
                button.targetGraphic.color = focused ? hoverBackground : !interactive ? disabledBackground : choiceSelected ? selectedBackground : idleFill;
                button.targetGraphic.CrossFadeColor(Color.white, 0, true, true);
            }
            lastCanInteract = interactive;
        }
        public void AdvanceHoverAnimation(float deltaTime) {
            if (!hasAuthoredScale || deltaTime <= 0) return;
            float desired = CanInteract && (hovered || selected) ? hoverScale : 1f;
            factor = Mathf.SmoothDamp(factor, desired, ref velocity, Mathf.Max(.04f, hoverSmoothTime), Mathf.Infinity, deltaTime);
            if (Mathf.Abs(factor - desired) < .0001f && Mathf.Abs(velocity) < .001f) { factor = desired; velocity = 0; }
            transform.localScale = authoredScale * factor;
        }
        void LateUpdate() { if (!Application.isPlaying) return; if (lastCanInteract != CanInteract) Paint(); AdvanceHoverAnimation(Time.unscaledDeltaTime); }
        void OnEnable() { ResetScale(); }
        void ResetScale() { factor = 1; velocity = 0; if (hasAuthoredScale) transform.localScale = authoredScale; }
        void OnDisable() { hovered = selected = false; ResetScale(); if (Application.isPlaying) Paint(); HoverChanged?.Invoke(false); }
    }
}

