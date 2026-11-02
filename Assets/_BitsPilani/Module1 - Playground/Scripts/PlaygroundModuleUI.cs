using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BitsPilani.Module1.Playground {
    [Serializable] public class PlaygroundChoiceSlot { public Button button; public TMP_Text label; }

    public sealed class PlaygroundModuleUI : MonoBehaviour {
        public Canvas mainCanvas, wristCanvas, thoughtCanvas, tintCanvas;
        public Transform head, leftHand;
        [HideInInspector] public TMP_Text sessionVideoStatus;
        public TMP_Text stepLabel, title, body, status, footer, hint;
        public TMP_Text primaryLabel, secondaryLabel, wristStep, wristStatus, thoughtText, questionCount;
        public Button primary, secondary, clueTool, guessTool, wristHelp, thoughtClose;
        // Retain serialized field names so the user's existing canvas references survive.
        [InspectorName("Next Question Button")] public Button skipQuestion;
        [InspectorName("Record Answer Button")] public Button recordingPlaceholder;
        public PlaygroundChoiceSlot[] choices;
        public Slider progress;
        public GameObject recordingGroup;
        public Image tint;
        [Min(1.3f)] public float panelDistance = 1.9f;
        public bool followParent = true;
        [Header("Comfortable panel following")]
        [Range(32, 65)] public float panelSideAngle = 44;
        [Range(.1f, 1f)] public float followSmoothTime = .35f;
        [Range(70, 120)] public float turnDeadZone = 85;
        [Tooltip("The panel settles on the opposite side of this story target when a new step opens.")]
        public Transform sceneFocus;
        public int VisibleChoiceCount { get; private set; }
        public string CurrentTitle => title ? title.text : "";
        bool initialized, followInitialized, expanded;
        TMP_Text recordAnswerLabel, nextQuestionLabel;
        int activeQuestion;
        Vector3 panelForward = Vector3.forward, followVelocity;
        float followedYaw, yawVelocity, side = 1;
        RectTransform background;
        RectTransform accent;
        Image statusBackground;
        Button hidePanel;
        PlaygroundPanelHover hover;

        public void Initialize() {
            if (initialized) return;
            initialized = true;
            recordingGroup.SetActive(false); thoughtCanvas.gameObject.SetActive(false); tintCanvas.gameObject.SetActive(false);
            foreach (var c in choices) c.button.gameObject.SetActive(false);
            recordingPlaceholder.interactable = false;
            recordingPlaceholder.gameObject.name = "Record or stop microphone answer";
            skipQuestion.gameObject.name = "Next reflection question";
            recordingGroup.name = "Step 8 microphone answers";
            if (progress && progress.fillRect) { progress.fillRect.sizeDelta = Vector2.zero; progress.fillRect.anchoredPosition = Vector2.zero; }
            ApplyStyle(); SetWristTools(false, false, null, null);
        }
        void ApplyStyle() {
            background = title.transform.parent as RectTransform;
            PlaygroundUIStyle.Surface(background.GetComponent<Image>(), PlaygroundUIStyle.Panel, true);
            var shadow = background.GetComponent<Shadow>(); if (!shadow) shadow = background.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, .02f, .04f, .30f); shadow.effectDistance = new Vector2(0, -8);
            var oldAccent = background.Find("Gold accent"); accent = oldAccent as RectTransform;
            if (accent) { accent.sizeDelta = new Vector2(92, 5); accent.GetComponent<Image>().color = PlaygroundUIStyle.Gold; accent.GetComponent<Image>().raycastTarget = false; }
            foreach (var text in mainCanvas.GetComponentsInChildren<TMP_Text>(true)) { text.color = PlaygroundUIStyle.Text; text.raycastTarget = false; }
            stepLabel.color = PlaygroundUIStyle.Mint; stepLabel.fontSize = 21; stepLabel.characterSpacing = 1.5f;
            title.fontSize = 38; title.fontStyle = FontStyles.Bold; title.enableAutoSizing = true; title.fontSizeMin = 30; title.fontSizeMax = 38;
            body.color = new Color(.85f, .92f, .94f); body.enableAutoSizing = true; body.fontSizeMin = 22; body.fontSizeMax = 28; body.overflowMode = TextOverflowModes.Overflow;
            footer.color = PlaygroundUIStyle.Muted; footer.enableAutoSizing = true; footer.fontSizeMin = 19; footer.fontSizeMax = 22;
            status.color = PlaygroundUIStyle.Mint; status.enableAutoSizing = true; status.fontSizeMin = 20; status.fontSizeMax = 24;
            hint.color = PlaygroundUIStyle.Muted; hint.fontSize = 18;
            statusBackground = NewSurface("Status inset", background, new Vector2(900, 70), Vector2.zero, new Color(.08f, .19f, .23f));
            statusBackground.transform.SetSiblingIndex(status.transform.GetSiblingIndex());
            PlaygroundUIStyle.Button(primary, primaryLabel, true); PlaygroundUIStyle.Button(secondary, secondaryLabel);
            foreach (var c in choices) PlaygroundUIStyle.Button(c.button, c.label);
            foreach (var button in new[] { clueTool, guessTool, wristHelp, thoughtClose, skipQuestion, recordingPlaceholder })
                PlaygroundUIStyle.Button(button, button.GetComponentInChildren<TMP_Text>(true), button == skipQuestion);
            if (wristCanvas) {
                PlaygroundUIStyle.Surface(wristStep.transform.parent.GetComponent<Image>(), PlaygroundUIStyle.Panel, true);
                wristStep.color = PlaygroundUIStyle.Gold; wristStatus.color = PlaygroundUIStyle.Text;
            }
            PlaygroundUIStyle.Surface(recordingGroup.GetComponent<Image>(), PlaygroundUIStyle.Card, true);
            PlaygroundUIStyle.Surface(thoughtText.transform.parent.GetComponent<Image>(), new Color(.94f, .97f, .94f), true);
            thoughtText.color = PlaygroundUIStyle.Ink;
            if (progress) {
                var track = progress.GetComponent<Image>(); if (track) { track.color = PlaygroundUIStyle.Card; track.raycastTarget = false; }
                var fill = progress.fillRect ? progress.fillRect.GetComponent<Image>() : null;
                if (fill) { fill.color = PlaygroundUIStyle.Mint; fill.raycastTarget = false; }
            }
            var hideImage = NewSurface("Hide instructions", background, new Vector2(130, 42), new Vector2(385, 423), Color.white);
            hideImage.raycastTarget = true; hidePanel = hideImage.gameObject.AddComponent<Button>(); hidePanel.targetGraphic = hideImage;
            var hideLabel = PlaygroundUIStyle.Label("Label", hideImage.transform, title.font, "Hide", new Vector2(116, 36), Vector2.zero, 22);
            PlaygroundUIStyle.Button(hidePanel, hideLabel); hidePanel.onClick.AddListener(() => mainCanvas.gameObject.SetActive(false));
            hover = mainCanvas.GetComponent<PlaygroundPanelHover>(); if (!hover) hover = mainCanvas.gameObject.AddComponent<PlaygroundPanelHover>();
            if (sessionVideoStatus && sessionVideoStatus.transform.IsChildOf(mainCanvas.transform)) {
                sessionVideoStatus.fontSize = 18; sessionVideoStatus.enableAutoSizing = true; sessionVideoStatus.fontSizeMin = 16; sessionVideoStatus.fontSizeMax = 18;
                sessionVideoStatus.overflowMode = TextOverflowModes.Ellipsis; sessionVideoStatus.textWrappingMode = TextWrappingModes.NoWrap;
                sessionVideoStatus.color = PlaygroundUIStyle.Muted; sessionVideoStatus.raycastTarget = false;
            }
            Layout(false, false); Recenter();
        }
        static Image NewSurface(string name, Transform parent, Vector2 size, Vector2 position, Color color) {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5; go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.sizeDelta = size; rect.anchoredPosition = position;
            var image = go.AddComponent<Image>(); PlaygroundUIStyle.Surface(image, color); image.raycastTarget = false; return image;
        }
        static void Place(RectTransform rect, Vector2 size, Vector2 position) { rect.sizeDelta = size; rect.anchoredPosition = position; }
        void Layout(bool large, bool cards) {
            expanded = large;
            float top = large ? 480 : 310;
            ((RectTransform)mainCanvas.transform).sizeDelta = new Vector2(1000, top * 2);
            Place(background, new Vector2(1000, top * 2), Vector2.zero);
            if (accent) accent.anchoredPosition = new Vector2(-404, top - 17);
            Place(stepLabel.rectTransform, new Vector2(710, 36), new Vector2(-95, top - 57));
            Place(title.rectTransform, new Vector2(900, 72), new Vector2(0, top - 115));
            ((RectTransform)hidePanel.transform).anchoredPosition = new Vector2(385, top - 57);
            Place(body.rectTransform, new Vector2(900, large ? cards ? 130 : 475 : 200), new Vector2(0, large ? cards ? 278 : 70 : 65));
            Place(status.rectTransform, new Vector2(864, large && !cards ? 68 : 58), new Vector2(0, large ? cards ? 180 : -224 : -90));
            Place(statusBackground.rectTransform, status.rectTransform.sizeDelta + new Vector2(36, 12), status.rectTransform.anchoredPosition);
            Place(footer.rectTransform, new Vector2(900, large ? 56 : 34), new Vector2(0, large ? -276 : -145));
            Place((RectTransform)progress.transform, new Vector2(900, 7), new Vector2(0, large ? -321 : -171));
            Place((RectTransform)primary.transform, new Vector2(510, 68), new Vector2(-195, large ? -378 : -215));
            Place((RectTransform)secondary.transform, new Vector2(350, 68), new Vector2(275, large ? -378 : -215));
            Place(hint.rectTransform, new Vector2(900, 24), new Vector2(0, large ? -450 : -289));
            if (sessionVideoStatus && sessionVideoStatus.transform.IsChildOf(mainCanvas.transform))
                Place(sessionVideoStatus.rectTransform, new Vector2(900, 20), new Vector2(0, large ? -424 : -263));
            for (int i = 0; i < choices.Length; i++) {
                Place((RectTransform)choices[i].button.transform, new Vector2(438, 112), new Vector2(i % 2 == 0 ? -231 : 231, 76 - i / 2 * 126));
                Place(choices[i].label.rectTransform, new Vector2(402, 92), Vector2.zero);
                choices[i].label.alignment = TextAlignmentOptions.MidlineLeft;
            }
        }
        public void SetStep(int step, string caption) {
            Initialize(); Recenter();
            stepLabel.text = step == 0 ? "MODULE 1  /  GETTING READY" : step > 9 ? "MODULE 1  /  COMPLETE" : "MODULE 1  /  STEP " + step + " OF 9";
            wristStep.text = step == 0 ? "GETTING READY" : step > 9 ? "MODULE COMPLETE" : "STEP " + step + "  /  " + caption;
        }
        public void Show(string heading, string description, string state, bool withChoices = false) {
            Initialize(); mainCanvas.gameObject.SetActive(true);
            title.text = heading; body.text = description; status.text = state; footer.text = "";
            Layout(withChoices || description.Length > 440, withChoices);
            hint.text = "Point with the right ray and release trigger. Hide clears the view; wrist Instructions reopens it.";
            recordingGroup.SetActive(false); DrawChoices(Array.Empty<string>(), null, null);
            SetPrimary("", null, false, false); SetSecondary("", null, false, false);
        }
        public void SetPrimary(string label, UnityAction callback, bool enabled = true, bool visible = true) {
            primary.gameObject.SetActive(visible); primaryLabel.text = label;
            primary.onClick.RemoveAllListeners(); if (callback != null) primary.onClick.AddListener(callback); primary.interactable = enabled && callback != null;
        }
        public void SetSecondary(string label, UnityAction callback, bool enabled = true, bool visible = true) {
            secondary.gameObject.SetActive(visible); secondaryLabel.text = label;
            secondary.onClick.RemoveAllListeners(); if (callback != null) secondary.onClick.AddListener(callback); secondary.interactable = enabled && callback != null;
        }
        public void DrawChoices(string[] labels, bool[] selected, Action<int> callback, bool enabled = true) {
            VisibleChoiceCount = Mathf.Min(labels.Length, choices.Length);
            for (int i = 0; i < choices.Length; i++) {
                var c = choices[i]; bool shown = i < VisibleChoiceCount; c.button.gameObject.SetActive(shown); c.button.onClick.RemoveAllListeners(); if (!shown) continue;
                bool picked = selected != null && i < selected.Length && selected[i];
                c.label.text = (picked ? "Selected\n" : "") + labels[i];
                c.button.interactable = enabled && callback != null; PlaygroundUIStyle.Button(c.button, c.label, false, picked);
                int index = i; if (callback != null) c.button.onClick.AddListener(() => callback(index));
            }
        }
        public void SetWristTools(bool clues, bool guesses, UnityAction onClues, UnityAction onGuesses) {
            clueTool.onClick.RemoveAllListeners(); guessTool.onClick.RemoveAllListeners();
            if (onClues != null) clueTool.onClick.AddListener(onClues); if (onGuesses != null) guessTool.onClick.AddListener(onGuesses);
            clueTool.interactable = clues && onClues != null; guessTool.interactable = guesses && onGuesses != null;
        }
        public void ShowRecordingQuestion(string question, int index, UnityAction onRecord, UnityAction onNext) {
            Show("Reflect on what you noticed", question, "Record and save an answer to continue."); Layout(true, false);
            Place(body.rectTransform, new Vector2(900, 220), new Vector2(0, 232));
            Place(status.rectTransform, new Vector2(864, 64), new Vector2(0, 68));
            Place(statusBackground.rectTransform, new Vector2(900, 76), new Vector2(0, 68));
            recordingGroup.SetActive(true); ((RectTransform)recordingGroup.transform).anchoredPosition = new Vector2(0, -112);
            activeQuestion = index + 1;
            recordAnswerLabel = recordingPlaceholder.GetComponentInChildren<TMP_Text>(true);
            nextQuestionLabel = skipQuestion.GetComponentInChildren<TMP_Text>(true);
            recordingPlaceholder.onClick.RemoveAllListeners(); if (onRecord != null) recordingPlaceholder.onClick.AddListener(onRecord);
            skipQuestion.onClick.RemoveAllListeners(); if (onNext != null) skipQuestion.onClick.AddListener(onNext);
            UpdateAnswerRecordingState(false, false, 0, 0);
        }
        public void UpdateAnswerRecordingState(bool recording, bool saving, int savedCount, float seconds, string error = null) {
            if (!recordAnswerLabel) recordAnswerLabel = recordingPlaceholder.GetComponentInChildren<TMP_Text>(true);
            if (!nextQuestionLabel) nextQuestionLabel = skipQuestion.GetComponentInChildren<TMP_Text>(true);
            recordAnswerLabel.text = saving ? "Saving answer..." : recording ? "Stop and save answer" : savedCount > 0 ? "Record another answer" : "Record answer";
            nextQuestionLabel.text = "Next";
            recordingPlaceholder.interactable = !saving;
            skipQuestion.interactable = savedCount > 0 && !recording && !saving;
            PlaygroundUIStyle.Button(skipQuestion, nextQuestionLabel, skipQuestion.interactable);
            PlaygroundUIStyle.Button(recordingPlaceholder, recordAnswerLabel, true);
            if (recording) { var colors = recordingPlaceholder.colors; colors.normalColor = PlaygroundUIStyle.Gold; recordingPlaceholder.colors = colors; }
            questionCount.text = "QUESTION " + activeQuestion + " OF 3   /   " + savedCount + (savedCount == 1 ? " CLIP SAVED" : " CLIPS SAVED");
            int elapsed = Mathf.FloorToInt(seconds);
            status.text = !string.IsNullOrEmpty(error) ? error : saving ? "Saving your microphone recording..." : recording
                ? "Recording " + (elapsed / 60).ToString("00") + ":" + (elapsed % 60).ToString("00") + ". Select Stop and save answer when finished."
                : savedCount > 0 ? "Answer saved. Record another clip, or select Next to continue." : "Select Record answer, speak, then select the same button to stop and save.";
            footer.text = "Microphone clips are saved beside your session video. Next requires at least one saved clip for this question.";
        }
        public void ShowThought(string text, UnityAction onClose) {
            thoughtText.text = text; thoughtCanvas.gameObject.SetActive(true); thoughtClose.onClick.RemoveAllListeners(); thoughtClose.onClick.AddListener(onClose);
        }
        public void SetTint(bool enabled) { tintCanvas.gameObject.SetActive(enabled); tint.color = new Color(1f, .64f, .18f, enabled ? .055f : 0f); }
        public void SetProgress(float value) { progress.value = Mathf.Clamp01(value); }
        public void Recenter() {
            if (!head) return;
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude > .1f) panelForward = forward;
            if (sceneFocus) { Vector3 target = sceneFocus.position - head.position; target.y = 0;
                float angle = Vector3.SignedAngle(panelForward, target, Vector3.up); if (Mathf.Abs(angle) > 8) side = angle > 0 ? -1 : 1;
            }
        }
        // Exposed for the Editor comfort check; frame-rate-independent and never alters the headset.
        public void UpdatePanelPose(float deltaTime) {
            if (!head || !mainCanvas || !followParent || !mainCanvas.gameObject.activeSelf) return;
            Vector3 look = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (look.sqrMagnitude > .1f && (!hover || !hover.IsHovered) && Vector3.Angle(panelForward, look) > turnDeadZone) panelForward = look;
            float desiredYaw = Quaternion.LookRotation(panelForward).eulerAngles.y + side * panelSideAngle;
            bool firstPlacement = !followInitialized;
            if (firstPlacement) { followedYaw = desiredYaw; followInitialized = true; }
            followedYaw = Mathf.SmoothDampAngle(followedYaw, desiredYaw, ref yawVelocity, Mathf.Max(.1f, followSmoothTime), 100f, deltaTime);
            Vector3 direction = Quaternion.Euler(0, followedYaw, 0) * Vector3.forward;
            Vector3 targetPosition = head.position + direction * Mathf.Max(1.3f, panelDistance) - Vector3.up * (expanded ? .20f : .27f);
            // Only first placement is immediate. Steps, turning and walking keep a continuous pose.
            if (firstPlacement) { mainCanvas.transform.position = targetPosition; mainCanvas.transform.rotation = Quaternion.LookRotation(targetPosition - head.position, Vector3.up); }
            mainCanvas.transform.position = Vector3.SmoothDamp(mainCanvas.transform.position, targetPosition, ref followVelocity, Mathf.Max(.1f, followSmoothTime), 4f, deltaTime);
            Vector3 away = mainCanvas.transform.position - head.position;
            if (away.sqrMagnitude > .001f) mainCanvas.transform.rotation = Quaternion.Slerp(mainCanvas.transform.rotation, Quaternion.LookRotation(away, Vector3.up), 1 - Mathf.Exp(-deltaTime / Mathf.Max(.1f, followSmoothTime)));
        }
        void LateUpdate() {
            if (!Application.isPlaying || !head) return; UpdatePanelPose(Time.unscaledDeltaTime);
            if (wristCanvas) { Vector3 away = wristCanvas.transform.position - head.position; if (away.sqrMagnitude > .001f) wristCanvas.transform.rotation = Quaternion.Slerp(wristCanvas.transform.rotation, Quaternion.LookRotation(away, head.up), 1 - Mathf.Exp(-Time.unscaledDeltaTime * 14)); }
            if (thoughtCanvas && thoughtCanvas.gameObject.activeSelf) { Vector3 away = thoughtCanvas.transform.position - head.position; away.y = 0; if (away.sqrMagnitude > .001f) thoughtCanvas.transform.rotation = Quaternion.Slerp(thoughtCanvas.transform.rotation, Quaternion.LookRotation(away, Vector3.up), 1 - Mathf.Exp(-Time.unscaledDeltaTime * 10)); }
        }
    }
}
