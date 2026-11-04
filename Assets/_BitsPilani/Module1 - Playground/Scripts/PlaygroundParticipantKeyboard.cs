using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace BitsPilani.Module1.Playground {
    [Serializable] public class PlaygroundKeyboardKey { public string key; public Button button; public TMP_Text label; }
    public sealed class PlaygroundParticipantKeyboard : MonoBehaviour, IPointerClickHandler, ISelectHandler {
        public TMP_InputField input;
        public PlaygroundSessionRecording recording;
        public Transform head;
        [Header("Scene-authored keyboard")]
        public Canvas keyboardCanvas;
        public TMP_Text preview;
        public Button doneButton;
        public PlaygroundKeyboardKey[] keys;
        [TextArea] public string emptyPreviewText = "Your participant ID";
        [TextArea] public string invalidIdText = "Use at least 3 letters or numbers. No spaces or symbols.";
        public bool IsOpen { get; private set; }
        public Canvas KeyboardCanvas => keyboardCanvas;
        public readonly List<Button> KeyButtons = new List<Button>();
        bool upper = true, bound;
        void Awake() { BindAuthoredKeys(); }
        public void BindAuthoredKeys() {
            if (bound || !keyboardCanvas || keys == null) return;
            bound = true; KeyButtons.Clear();
            foreach (var entry in keys) {
                if (!entry.button) continue;
                var key = entry.key; KeyButtons.Add(entry.button);
                entry.button.onClick.AddListener(() => { if (key == "done") Done(); else if (key == "close") Close(); else Press(key); });
            }
            if (input) input.onValueChanged.AddListener(_ => RefreshValidity());
            RefreshValidity();
        }
        public void OnPointerClick(PointerEventData data) { Open(); }
        public void OnSelect(BaseEventData data) { Open(); }
        public void Open() {
            if (!input) input = GetComponent<TMP_InputField>();
            if (!input || !input.interactable || recording && (!recording.showVirtualKeyboard || recording.IsRecording || recording.IsSaving)) return;
            BindAuthoredKeys();
            if (!keyboardCanvas) { Debug.LogError("Assign the editable keyboard canvas saved in the scene.", this); return; }
            IsOpen = true; keyboardCanvas.gameObject.SetActive(true); input.shouldHideSoftKeyboard = true; RefreshValidity();
        }
        public void RefreshValidity() {
            if (doneButton) doneButton.interactable = input && PlaygroundSessionRecording.IsValidParticipantId(input.text);
            if (preview) preview.text = input && !string.IsNullOrEmpty(input.text) ? input.text : emptyPreviewText;
        }
        public void Press(string key) {
            if (!IsOpen || !input || !input.interactable) return;
            string value = input.text;
            if (key == "shift") {
                upper = !upper;
                foreach (var entry in keys) if (entry.label && entry.key.Length == 1 && char.IsLetter(entry.key[0])) entry.label.text = upper ? entry.key.ToUpperInvariant() : entry.key.ToLowerInvariant();
                return;
            }
            if (key == "backspace") { if (value.Length > 0) value = value.Substring(0, value.Length - 1); }
            else if (key.Length == 1 && char.IsLetterOrDigit(key[0]) && value.Length < 64) value += upper ? key.ToUpperInvariant() : key.ToLowerInvariant();
            input.SetTextWithoutNotify(value); input.onValueChanged.Invoke(value); input.caretPosition = value.Length; input.ForceLabelUpdate(); RefreshValidity();
        }
        public void Done() {
            if (!input || !PlaygroundSessionRecording.IsValidParticipantId(input.text)) { RefreshValidity(); if (preview) preview.text = invalidIdText; return; }
            Close(); if (recording) recording.SetParticipantId(input.text); else input.onEndEdit.Invoke(input.text);
        }
        public void Close() { IsOpen = false; if (keyboardCanvas) keyboardCanvas.gameObject.SetActive(false); }
        void OnDisable() { Close(); }
    }
}
