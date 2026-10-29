using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BitsPilani.Module1.Playground {
    [Serializable] public class PlaygroundChoiceSlot {
        public Button button;
        public TMP_Text label;
    }

    public sealed class PlaygroundModuleUI : MonoBehaviour {
        public Canvas mainCanvas, wristCanvas, thoughtCanvas, tintCanvas;
        public Transform head, leftHand;
        public TMP_Text stepLabel, title, body, status, footer, hint;
        public TMP_Text primaryLabel, secondaryLabel, wristStep, wristStatus, thoughtText, questionCount;
        public Button primary, secondary, clueTool, guessTool, wristHelp, thoughtClose, skipQuestion, recordingPlaceholder;
        public PlaygroundChoiceSlot[] choices;
        public Slider progress;
        public GameObject recordingGroup;
        public Image tint;
        public float panelDistance = 1.9f;
        public bool followParent = true;
        public int VisibleChoiceCount { get; private set; }
        public string CurrentTitle => title ? title.text : "";
        bool initialized;
        Vector3 panelForward = Vector3.forward;
        float panelSideOffset = 1.8f;
        readonly Color baseChoice = new Color(.12f, .22f, .28f);
        readonly Color chosenChoice = new Color(.10f, .43f, .39f);

        public void Initialize() {
            if (initialized) return;
            initialized = true;
            recordingGroup.SetActive(false); thoughtCanvas.gameObject.SetActive(false); tintCanvas.gameObject.SetActive(false);
            foreach (var c in choices) c.button.gameObject.SetActive(false);
            recordingPlaceholder.interactable = false;
            if (progress && progress.fillRect) { progress.fillRect.sizeDelta = Vector2.zero; progress.fillRect.anchoredPosition = Vector2.zero; }
            SetWristTools(false, false, null, null);
        }

        public void SetStep(int step, string caption) {
            Recenter();
            stepLabel.text = step == 0 ? "MODULE 1  /  GETTING READY" : step > 9 ? "MODULE 1  /  COMPLETE" : "MODULE 1  /  STEP " + step + " OF 9";
            wristStep.text = step == 0 ? "GETTING READY" : step > 9 ? "MODULE COMPLETE" : "STEP " + step + "  /  " + caption;
        }

        public void Show(string heading, string description, string state, bool withChoices = false) {
            Initialize();
            mainCanvas.gameObject.SetActive(true); title.text = heading; body.text = description; status.text = state; footer.text = "";
            panelSideOffset = withChoices ? .3f : 1.8f;
            body.rectTransform.sizeDelta = new Vector2(900, withChoices ? 140 : 360);
            body.rectTransform.anchoredPosition = new Vector2(0, withChoices ? 280 : 155);
            status.rectTransform.anchoredPosition = new Vector2(0, withChoices ? 176 : -85);
            status.rectTransform.sizeDelta = new Vector2(900, 64); status.fontSize = 24;
            recordingGroup.SetActive(false);
            DrawChoices(Array.Empty<string>(), null, null);
            SetPrimary("", null, false, false); SetSecondary("", null, false, false);
        }

        public void SetPrimary(string label, UnityAction callback, bool enabled = true, bool visible = true) {
            primary.gameObject.SetActive(visible); primaryLabel.text = label;
            primary.onClick.RemoveAllListeners(); if (callback != null) primary.onClick.AddListener(callback);
            primary.interactable = enabled && callback != null;
        }
        public void SetSecondary(string label, UnityAction callback, bool enabled = true, bool visible = true) {
            secondary.gameObject.SetActive(visible); secondaryLabel.text = label;
            secondary.onClick.RemoveAllListeners(); if (callback != null) secondary.onClick.AddListener(callback);
            secondary.interactable = enabled && callback != null;
        }

        public void DrawChoices(string[] labels, bool[] selected, Action<int> callback, bool enabled = true) {
            VisibleChoiceCount = Mathf.Min(labels.Length, choices.Length);
            for (int i = 0; i < choices.Length; i++) {
                var c = choices[i]; bool shown = i < labels.Length;
                c.button.gameObject.SetActive(shown); c.button.onClick.RemoveAllListeners();
                if (!shown) continue;
                bool picked = selected != null && i < selected.Length && selected[i];
                c.label.text = (picked ? "[Selected]  " : "") + labels[i];
                var image = c.button.targetGraphic as Image; if (image) image.color = picked ? chosenChoice : baseChoice;
                int index = i; if (callback != null) c.button.onClick.AddListener(() => callback(index));
                c.button.interactable = enabled && callback != null;
            }
        }

        public void SetWristTools(bool clues, bool guesses, UnityAction onClues, UnityAction onGuesses) {
            clueTool.onClick.RemoveAllListeners(); guessTool.onClick.RemoveAllListeners();
            if (onClues != null) clueTool.onClick.AddListener(onClues);
            if (onGuesses != null) guessTool.onClick.AddListener(onGuesses);
            clueTool.interactable = clues && onClues != null; guessTool.interactable = guesses && onGuesses != null;
        }

        public void ShowRecordingQuestion(string question, int index, UnityAction onSkip) {
            Show("Reflect on what you noticed", question, "Spoken answers will be added later. Use Skip to continue for now.");
            panelSideOffset = .3f;
            status.rectTransform.anchoredPosition = new Vector2(0, -65);
            status.rectTransform.sizeDelta = new Vector2(900, 50); status.fontSize = 22;
            recordingGroup.SetActive(true); recordingPlaceholder.interactable = false;
            questionCount.text = "QUESTION " + (index + 1) + " OF 3";
            skipQuestion.onClick.RemoveAllListeners(); skipQuestion.onClick.AddListener(onSkip);
            skipQuestion.interactable = true;
            footer.text = "No microphone is accessed and no recording is made in this prototype.";
        }

        public void ShowThought(string text, UnityAction onClose) {
            thoughtText.text = text; thoughtCanvas.gameObject.SetActive(true);
            thoughtClose.onClick.RemoveAllListeners(); thoughtClose.onClick.AddListener(onClose);
        }

        public void SetTint(bool enabled) {
            tintCanvas.gameObject.SetActive(enabled);
            tint.color = new Color(1f, .64f, .18f, enabled ? .055f : 0f);
        }

        public void SetProgress(float value) { progress.value = Mathf.Clamp01(value); }

        public void Recenter() {
            if (!head) return;
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude > .1f) panelForward = forward;
        }

        void LateUpdate() {
            if (!Application.isPlaying || !head) return;
            if (followParent && mainCanvas && mainCanvas.gameObject.activeSelf) {
                Vector3 look = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
                if (look.sqrMagnitude > .1f && Vector3.Angle(panelForward, look) > 95f) Recenter();
                Vector3 forward = panelForward;
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                // Keep the large panel to the right so it does not cover the child or gaze targets.
                mainCanvas.transform.position = head.position + forward * panelDistance + right * panelSideOffset - Vector3.up * .18f;
                mainCanvas.transform.rotation = Quaternion.LookRotation(mainCanvas.transform.position - head.position, Vector3.up);
            }
            if (wristCanvas) {
                Vector3 away = wristCanvas.transform.position - head.position;
                if (away.sqrMagnitude > .001f) wristCanvas.transform.rotation = Quaternion.LookRotation(away, head.up);
            }
            if (thoughtCanvas && thoughtCanvas.gameObject.activeSelf) {
                Vector3 away = thoughtCanvas.transform.position - head.position; away.y = 0;
                if (away.sqrMagnitude > .001f) thoughtCanvas.transform.rotation = Quaternion.LookRotation(away, Vector3.up);
            }
        }
    }
}
