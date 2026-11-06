using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BitsPilani.Module1.Playground {
    [Serializable] public class PlaygroundChoiceSlot { public Button button; public TMP_Text label; }

    [Serializable] public class PlaygroundSummaryRow { public TMP_Text firstAttempt, replay; }

    public sealed class PlaygroundModuleUI : MonoBehaviour {
        public PlaygroundUITextCatalog textCatalog;
        public string Copy(string id) => textCatalog ? textCatalog.Get(id) : id;
        public Canvas mainCanvas, wristCanvas, thoughtCanvas, tintCanvas;
        [Header("Authored replay alert and summary")]
        public Canvas replayAttentionCanvas;
        [Min(1)] public float replayAttentionSeconds = 5;
        public GameObject summaryTableRoot;
        [Tooltip("Rows: response, timing, outcome, clues/guesses.")]
        public PlaygroundSummaryRow[] summaryRows;
        float replayAttentionUntil;
        bool questionVoiceoverPlaying, answerUserSaved, answerRecording, answerSaving;
        int answerSavedCount;
        public void SetAnswerPrerequisites(bool narrationPlaying, bool userSaved) {
            questionVoiceoverPlaying = narrationPlaying; answerUserSaved = userSaved; ApplyAnswerButtonGates();
        }
        void ApplyAnswerButtonGates() {
            recordingPlaceholder.interactable = !questionVoiceoverPlaying && !answerSaving;
            skipQuestion.interactable = !questionVoiceoverPlaying && answerUserSaved && answerSavedCount > 0 && !answerRecording && !answerSaving;
        }
        public void ShowReplayAttention() {
            if (!replayAttentionCanvas) { Debug.LogWarning("[M1 UI] Assign the scene-authored replay attention canvas.", this); return; }
            replayAttentionCanvas.gameObject.SetActive(true); replayAttentionUntil = Time.unscaledTime + replayAttentionSeconds;
        }
        public void HideReplayAttention() { if (replayAttentionCanvas) replayAttentionCanvas.gameObject.SetActive(false); }
        public void UpdateReplayAttention(float unscaledTime) { if (replayAttentionCanvas && replayAttentionCanvas.gameObject.activeSelf && unscaledTime >= replayAttentionUntil) HideReplayAttention(); }
        [ContextMenu("Preview layout/Summary comparison table")]
        public void PreviewSummaryTable() {
            recordingGroup.SetActive(false);
            foreach (var choice in choices) choice.button.gameObject.SetActive(false);
            if (narrativeScroll) narrativeScroll.gameObject.SetActive(false);
            if (narrativeUp) narrativeUp.gameObject.SetActive(false); if (narrativeDown) narrativeDown.gameObject.SetActive(false);
            if (summaryTableRoot) summaryTableRoot.SetActive(true);
        }
        public void ShowAttemptComparison(PlaygroundSessionData data, string audioSummary) {
            Show(Copy("ui_dc59cb9d06"), "", Copy("ui_c5747a2648"));
            if (!summaryTableRoot || summaryRows == null || summaryRows.Length != 4) {
                body.text = Copy("ui_0a356fd6ac") + data.firstResponse + "\n" + data.firstOutcome + Copy("ui_ff6e6a9ba5") + data.replayResponse + "\n" + data.replayOutcome; RefreshNarrative(); return;
            }
            string firstTiming = data.firstResponseSeconds < 0 ? Copy("ui_f2292b7b38") : data.firstResponseSeconds.ToString("F1") + Copy("ui_508613acea");
            bool replayResponse = !string.IsNullOrEmpty(data.replayResponse) && data.replayResponse != "No response";
            string secondTiming = !replayResponse ? Copy("ui_81a9e3ba15") : data.replaySecondsBeforePop > 0 ? data.replaySecondsBeforePop.ToString("F1") + Copy("ui_b537a6b181") : Copy("ui_f05a7a3556");
            string[] first = { data.firstResponse, firstTiming, data.firstOutcome, (data.clues.Count == 0 ? noCluesSelectedText : data.clues.Count + (data.clues.Count == 1 ? Copy("ui_d2e0c3762f") : Copy("ui_d05260aeaa"))) + "\n" + (data.guesses.Count == 0 ? noGuessesSelectedText : data.guesses.Count + (data.guesses.Count == 1 ? Copy("ui_9eecdf20a0") : Copy("ui_5f8a84dbe9"))) };
            string[] second = { data.replayResponse, secondTiming, data.replayOutcome, Copy("ui_f48147d4c7") };
            for (int i = 0; i < 4; i++) { summaryRows[i].firstAttempt.text = first[i]; summaryRows[i].replay.text = second[i]; }
            PreviewSummaryTable(); footer.text = audioSummary; HideReplayAttention();
        }
        [Header("Scene-authored canvas")]
        [Tooltip("Keep enabled to preserve the canvas transforms, colors, fonts and layout you edit outside Play mode. Step text and button availability still update.")]
        public bool useAuthoredCanvas;
        [SerializeField, HideInInspector] RectTransform authoredBackground, authoredAccent;
        [SerializeField, HideInInspector] Image authoredStatusBackground;
        [SerializeField, HideInInspector] Button authoredHidePanel;
        public Transform head, leftHand;
        [Tooltip("Keep the saved panel offset above the hand independent of hand roll/pitch/yaw. Its position is captured from your scene when the module initializes.")]
        public bool stabilizeWristAboveHand = true;
        Transform wristReferenceFrame;
        Vector3 stableWristOffset;
        bool wristPlacementCaptured;
        [HideInInspector] public TMP_Text sessionVideoStatus;
        public TMP_Text stepLabel, title, body, status, footer, hint;
        public TMP_Text primaryLabel, secondaryLabel, wristStep, wristStatus, thoughtText, questionCount;
        public Button primary, secondary, clueTool, guessTool, wristHelp, thoughtClose;
        [Header("Whole-step skip (scene-authored header button)")]
        public Button skipStep;
        public string noCluesSelectedText = "No clues selected";
        public string noGuessesSelectedText = "No guesses selected";
        // Retain serialized field names so the user's existing canvas references survive.
        [InspectorName("Next Question Button")] public Button skipQuestion;
        [InspectorName("Record Answer Button")] public Button recordingPlaceholder;
        public PlaygroundChoiceSlot[] choices;
        public Slider progress;
        [Header("Landscape narrative and child-view return")]
        public ScrollRect narrativeScroll;
        public Button narrativeUp, narrativeDown, wristReturn;
        public Scrollbar narrativeScrollbar;
        [Header("Narrative layout variants (canvas pixels)")]
        public Vector2 textOnlyNarrativeSize = new Vector2(1740, 500);
        public Vector2 textOnlyNarrativePosition = new Vector2(0, 80);
        public Vector2 selectionNarrativeSize = new Vector2(820, 500);
        public Vector2 selectionNarrativePosition = new Vector2(-465, 80);
                public void SetNarrativeColumns(bool split) {
            if (!narrativeScroll) return;
            var rect = (RectTransform)narrativeScroll.transform;
            rect.sizeDelta = split ? selectionNarrativeSize : textOnlyNarrativeSize;
            rect.anchoredPosition = split ? selectionNarrativePosition : textOnlyNarrativePosition;

            RefreshNarrative();
        }
        [ContextMenu("Preview layout/Text only (full width)")]
        public void PreviewTextOnlyLayout() {
            if (summaryTableRoot) summaryTableRoot.SetActive(false); if (narrativeScroll) narrativeScroll.gameObject.SetActive(true);
            recordingGroup.SetActive(false);
            foreach (var choice in choices) choice.button.gameObject.SetActive(false);
            SetNarrativeColumns(false);
        }
        [ContextMenu("Preview layout/Choices (two columns)")]
        public void PreviewChoiceLayout() {
            if (summaryTableRoot) summaryTableRoot.SetActive(false); if (narrativeScroll) narrativeScroll.gameObject.SetActive(true);
            recordingGroup.SetActive(false);
            foreach (var choice in choices) choice.button.gameObject.SetActive(true);
            SetNarrativeColumns(true);
        }
        public void RefreshNarrative() {
            if (!narrativeScroll || !body || !narrativeScroll.viewport) return;
            Canvas.ForceUpdateCanvases();
            var viewport = narrativeScroll.viewport;
            float width = Mathf.Max(100, body.rectTransform.rect.width);
            float height = Mathf.Max(viewport.rect.height, body.GetPreferredValues(body.text, width, Mathf.Infinity).y + 24);
            body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            body.rectTransform.anchoredPosition = new Vector2(body.rectTransform.anchoredPosition.x, 0);
            Canvas.ForceUpdateCanvases(); narrativeScroll.verticalNormalizedPosition = 1;
            UpdateNarrativeButtons();
        }
        void UpdateNarrativeButtons() {
            if (!narrativeScroll || !narrativeScroll.viewport || !narrativeScroll.content) return;
            bool overflow = narrativeScroll.content.rect.height > narrativeScroll.viewport.rect.height + 1;
            narrativeScroll.vertical = overflow;
            float position = narrativeScroll.verticalNormalizedPosition;
            if (narrativeUp) narrativeUp.gameObject.SetActive(false);
            if (narrativeDown) narrativeDown.gameObject.SetActive(false);
            if (narrativeScrollbar) { narrativeScrollbar.size = Mathf.Clamp01(narrativeScroll.viewport.rect.height / Mathf.Max(1, narrativeScroll.content.rect.height)); narrativeScrollbar.gameObject.SetActive(overflow); narrativeScrollbar.interactable = overflow; }
        }
        void ScrollNarrative(float amount) {
            if (!narrativeScroll) return;
            narrativeScroll.verticalNormalizedPosition = Mathf.Clamp01(narrativeScroll.verticalNormalizedPosition + amount);
            UpdateNarrativeButtons();
        }
        public void SetChildViewReturn(bool visible, bool ready, UnityAction callback) {
            if (wristCanvas && visible) wristCanvas.gameObject.SetActive(true);
            if (!wristReturn) { if (visible) { SetWristShortcut(ready ? Copy("ui_3c347c3e65") : Copy("ui_8936210f4c"), callback); wristHelp.interactable = ready; } return; }
            wristReturn.gameObject.SetActive(visible); wristHelp.gameObject.SetActive(!visible);
            if (visible) wristHelp.interactable = false;
            wristReturn.onClick.RemoveAllListeners(); if (callback != null) wristReturn.onClick.AddListener(callback);
            wristReturn.interactable = visible && ready && callback != null;
            var label = wristReturn.GetComponentInChildren<TMP_Text>(true);
            if (label) label.text = ready ? Copy("ui_3c347c3e65") : Copy("ui_67b6f9c3fd");
        }
        public GameObject recordingGroup;
        public Image tint;
        [Min(1.3f)] public float panelDistance = 1.9f;
        [Tooltip("Leave disabled for the fixed world-space panel near the child.")]
        public bool followParent = false;
        [Header("Fixed panel placement")]
        public Transform panelReferenceChild;
        [Tooltip("Optional authored world-space pose. If empty, the panel is placed behind/right of the child's starting position.")]
        public Transform fixedPanelAnchor;
        [Tooltip("Metres: right of the child, height above the child's ground, behind the child as seen from the initial parent position.")]
        public Vector3 fixedPanelOffset = new Vector3(1.35f, 1.65f, 1.35f);
        [Min(.0005f)] public float fixedPanelScale = .00135f;
        bool fixedPanelPlaced;
        Vector3 fixedPanelPosition;
        Quaternion fixedPanelRotation;
        [Header("Comfortable panel following")]
        [Range(32, 65)] public float panelSideAngle = 44;
        [Range(.1f, 1f)] public float followSmoothTime = .35f;
        [Range(70, 120)] public float turnDeadZone = 85;
        [Tooltip("The panel settles on the opposite side of this story target when a new step opens.")]
        public Transform sceneFocus;
        public int VisibleChoiceCount { get; private set; }
        public string CurrentTitle => title ? title.text : "";
        bool initialized, followInitialized, expanded;
        bool instructionStepActive;
        bool instructionsHiddenByUser;
        TMP_Text recordAnswerLabel, nextQuestionLabel;
        int activeQuestion;
        string recordingQuestion;
        Vector3 panelForward = Vector3.forward, followVelocity;
        float followedYaw, yawVelocity, side = 1;
        RectTransform background;
        RectTransform accent;
        Image statusBackground;
        Button hidePanel;
        PlaygroundPanelHover hover;

        public void Initialize() {
            if (initialized) return;
            initialized = true; CaptureWristPlacement();
            HideReplayAttention(); if (summaryTableRoot) summaryTableRoot.SetActive(false);
            recordingGroup.SetActive(false); thoughtCanvas.gameObject.SetActive(false); tintCanvas.gameObject.SetActive(false);
            foreach (var c in choices) c.button.gameObject.SetActive(false);
            recordingPlaceholder.interactable = false;
            recordingPlaceholder.gameObject.name = "Record or stop microphone answer";
            skipQuestion.gameObject.name = "Next reflection question";
            recordingGroup.name = "Step 8 microphone answers";
            if (!useAuthoredCanvas && progress && progress.fillRect) { progress.fillRect.sizeDelta = Vector2.zero; progress.fillRect.anchoredPosition = Vector2.zero; }
            if (useAuthoredCanvas || Application.isPlaying) BindAuthoredCanvas(); else ApplyStyle();
            SetWristTools(false, false, null, null); wristHelp.interactable = false;
        }
        void BindAuthoredCanvas() {
            background = authoredBackground; accent = authoredAccent;
            statusBackground = authoredStatusBackground; hidePanel = authoredHidePanel;
            hover = mainCanvas.GetComponent<PlaygroundPanelHover>();
            if (hidePanel) hidePanel.onClick.AddListener(HideInstructions);
            if (narrativeUp) narrativeUp.onClick.AddListener(() => ScrollNarrative(.5f));
            if (narrativeDown) narrativeDown.onClick.AddListener(() => ScrollNarrative(-.5f));
            if (narrativeScroll) narrativeScroll.onValueChanged.AddListener(_ => UpdateNarrativeButtons());
            if (narrativeScrollbar) narrativeScrollbar.onValueChanged.AddListener(value => { if (narrativeScroll) narrativeScroll.verticalNormalizedPosition = value; });
        }
        public void HideInstructions() { instructionsHiddenByUser = true; mainCanvas.gameObject.SetActive(false); UpdateInstructionButton(); }
        void UpdateInstructionButton() { if (wristHelp) wristHelp.interactable = initialized && instructionStepActive && (!wristReturn || !wristReturn.gameObject.activeSelf) && !mainCanvas.gameObject.activeSelf; }
        // Called explicitly once by the Editor authoring tool, never on entering Play mode.
        public void BakeCanvasForEditing() {
            if (useAuthoredCanvas) return;
            Initialize(); Layout(true, true);
            ((RectTransform)mainCanvas.transform).sizeDelta = new Vector2(1000, 1100);
            background.sizeDelta = new Vector2(1000, 1100);
            if (accent) accent.anchoredPosition = new Vector2(-404, 533);
            stepLabel.rectTransform.anchoredPosition = new Vector2(-95, 493);
            title.rectTransform.anchoredPosition = new Vector2(0, 435);
            ((RectTransform)hidePanel.transform).anchoredPosition = new Vector2(385, 493);
            Place(body.rectTransform, new Vector2(900, 180), new Vector2(0, 300));
            // All step variants share these editable slots; only their contents/visibility change.
            foreach (var c in choices) c.button.gameObject.SetActive(true);
            mainCanvas.gameObject.SetActive(true);
            authoredBackground = background; authoredAccent = accent;
            authoredStatusBackground = statusBackground; authoredHidePanel = hidePanel;
            useAuthoredCanvas = true;
        }
        void StyleButton(Button button, TMP_Text label, bool primary = false, bool selected = false) {
            if (!useAuthoredCanvas && !Application.isPlaying) PlaygroundUIStyle.Button(button, label, primary, selected);
        }
        void ApplyStyle() {
            background = title.transform.parent as RectTransform;
            PlaygroundUIStyle.Surface(background.GetComponent<Image>(), PlaygroundUIStyle.Panel, true);
            PlaygroundUIStyle.CardShadow(background.GetComponent<Image>(), .12f, 6f);
            var oldAccent = background.Find("Gold accent"); accent = oldAccent as RectTransform;
            if (accent) { accent.sizeDelta = new Vector2(92, 5); accent.GetComponent<Image>().color = PlaygroundUIStyle.Gold; accent.GetComponent<Image>().raycastTarget = false; }
            foreach (var text in mainCanvas.GetComponentsInChildren<TMP_Text>(true)) { text.color = PlaygroundUIStyle.Text; text.raycastTarget = false; }
            stepLabel.color = PlaygroundUIStyle.Mint; stepLabel.fontSize = 21; stepLabel.characterSpacing = 1.5f;
            title.fontSize = 38; title.fontStyle = FontStyles.Bold; title.enableAutoSizing = true; title.fontSizeMin = 30; title.fontSizeMax = 38;
            body.color = PlaygroundUIStyle.Text; body.enableAutoSizing = true; body.fontSizeMin = 22; body.fontSizeMax = 28; body.overflowMode = TextOverflowModes.Overflow;
            footer.color = PlaygroundUIStyle.Muted; footer.enableAutoSizing = true; footer.fontSizeMin = 19; footer.fontSizeMax = 22;
            status.color = PlaygroundUIStyle.Mint; status.enableAutoSizing = true; status.fontSizeMin = 20; status.fontSizeMax = 24;
            hint.color = PlaygroundUIStyle.Muted; hint.fontSize = 18;
            statusBackground = NewSurface("Status inset", background, new Vector2(900, 70), Vector2.zero, PlaygroundUIStyle.Inset);
            statusBackground.transform.SetSiblingIndex(status.transform.GetSiblingIndex());
            PlaygroundUIStyle.Button(primary, primaryLabel, true); PlaygroundUIStyle.Button(secondary, secondaryLabel);
            foreach (var c in choices) PlaygroundUIStyle.Button(c.button, c.label);
            foreach (var button in new[] { clueTool, guessTool, wristHelp, thoughtClose, skipQuestion, recordingPlaceholder })
                PlaygroundUIStyle.Button(button, button.GetComponentInChildren<TMP_Text>(true), button == skipQuestion);
            if (wristCanvas) {
                PlaygroundUIStyle.Surface(wristStep.transform.parent.GetComponent<Image>(), PlaygroundUIStyle.Panel, true);
                PlaygroundUIStyle.CardShadow(wristStep.transform.parent.GetComponent<Image>());
                wristStep.color = PlaygroundUIStyle.Gold; wristStatus.color = PlaygroundUIStyle.Text;
            }
            PlaygroundUIStyle.Surface(recordingGroup.GetComponent<Image>(), PlaygroundUIStyle.Card, true);
            PlaygroundUIStyle.CardShadow(recordingGroup.GetComponent<Image>(), .10f, 3f);
            PlaygroundUIStyle.Surface(thoughtText.transform.parent.GetComponent<Image>(), Color.white, true);
            PlaygroundUIStyle.CardShadow(thoughtText.transform.parent.GetComponent<Image>(), .17f, 8f);
            thoughtText.color = PlaygroundUIStyle.Ink;
            if (progress) {
                var track = progress.GetComponent<Image>(); if (track) { track.color = new Color(.9f, .9f, .9f); track.raycastTarget = false; }
                var fill = progress.fillRect ? progress.fillRect.GetComponent<Image>() : null;
                if (fill) { fill.color = PlaygroundUIStyle.Mint; fill.raycastTarget = false; }
            }
            var hideImage = NewSurface("Hide instructions", background, new Vector2(130, 42), new Vector2(385, 423), Color.white);
            hideImage.raycastTarget = true; hidePanel = hideImage.gameObject.AddComponent<Button>(); hidePanel.targetGraphic = hideImage;
            var hideLabel = PlaygroundUIStyle.Label("Label", hideImage.transform, title.font, "Hide", new Vector2(116, 36), Vector2.zero, 22);
            PlaygroundUIStyle.Button(hidePanel, hideLabel); hidePanel.onClick.AddListener(HideInstructions);
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
            var image = go.AddComponent<Image>(); PlaygroundUIStyle.Surface(image, color); PlaygroundUIStyle.CardShadow(image, .08f, 3f); image.raycastTarget = false; return image;
        }
        static void Place(RectTransform rect, Vector2 size, Vector2 position) { rect.sizeDelta = size; rect.anchoredPosition = position; }
        void Layout(bool large, bool cards) {
            if (useAuthoredCanvas) return;
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
            Initialize(); instructionStepActive = step > 0; Recenter(); SetChildViewReturn(false, false, null); SetWristShortcut(Copy("ui_548442ddeb"), ShowFixedInstructions);
            stepLabel.text = step == 0 ? Copy("ui_4530b7b026") : step > 9 ? Copy("ui_7b431b9c6c") : Copy("ui_6223752081") + step + Copy("ui_38c0c8830d");
            wristStep.text = step == 0 ? Copy("ui_419d0202db") : step > 9 ? Copy("ui_139325a34f") : Copy("ui_c5964dcff2") + step + "  /  " + caption;
        }
        public void Show(string heading, string description, string state, bool withChoices = false) {
            Initialize(); mainCanvas.gameObject.SetActive(!instructionsHiddenByUser); UpdateInstructionButton();
            if (summaryTableRoot) summaryTableRoot.SetActive(false); if (narrativeScroll) narrativeScroll.gameObject.SetActive(true);
            title.text = heading; body.text = description; status.text = state; footer.text = ""; RefreshNarrative();
            Layout(withChoices || description.Length > 440, withChoices);
            hint.text = Copy("ui_1fd2add495");
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
                c.label.text = labels[i];
                if (useAuthoredCanvas && c.button.TryGetComponent<PlaygroundButtonFeedback>(out var feedback)) feedback.SetChoiceSelected(picked);
                c.button.interactable = enabled && callback != null; StyleButton(c.button, c.label, false, picked);
                int index = i; if (callback != null) c.button.onClick.AddListener(() => callback(index));
            }
            SetNarrativeColumns(VisibleChoiceCount > 0 || recordingGroup.activeSelf);
        }
        public void SetWristTools(bool clues, bool guesses, UnityAction onClues, UnityAction onGuesses) {
            clueTool.onClick.RemoveAllListeners(); guessTool.onClick.RemoveAllListeners();
            if (onClues != null) clueTool.onClick.AddListener(onClues); if (onGuesses != null) guessTool.onClick.AddListener(onGuesses);
            clueTool.interactable = clues && onClues != null; guessTool.interactable = guesses && onGuesses != null;
        }
        public void ShowRecordingQuestion(string question, int index, UnityAction onRecord, UnityAction onNext, bool narrationPlaying = false) {
            Show(Copy("ui_40746e3394"), question, Copy("ui_08066747df")); Layout(true, false);
            if (!useAuthoredCanvas) {
                Place(body.rectTransform, new Vector2(900, 220), new Vector2(0, 232));
                Place(status.rectTransform, new Vector2(864, 64), new Vector2(0, 68));
                Place(statusBackground.rectTransform, new Vector2(900, 76), new Vector2(0, 68));
                ((RectTransform)recordingGroup.transform).anchoredPosition = new Vector2(0, -112);
            }
            recordingGroup.SetActive(true); SetNarrativeColumns(true);
            activeQuestion = index + 1; recordingQuestion = question;
            recordAnswerLabel = recordingPlaceholder.GetComponentInChildren<TMP_Text>(true);
            nextQuestionLabel = skipQuestion.GetComponentInChildren<TMP_Text>(true);
            recordingPlaceholder.onClick.RemoveAllListeners(); if (onRecord != null) recordingPlaceholder.onClick.AddListener(onRecord);
            skipQuestion.onClick.RemoveAllListeners(); if (onNext != null) skipQuestion.onClick.AddListener(onNext);
            SetAnswerPrerequisites(narrationPlaying, false); UpdateAnswerRecordingState(false, false, 0, 0);
        }
        public void UpdateAnswerRecordingState(bool recording, bool saving, int savedCount, float seconds, string error = null) {
            if (!recordAnswerLabel) recordAnswerLabel = recordingPlaceholder.GetComponentInChildren<TMP_Text>(true);
            if (!nextQuestionLabel) nextQuestionLabel = skipQuestion.GetComponentInChildren<TMP_Text>(true);
            recordAnswerLabel.text = saving ? Copy("ui_dbfd6f2663") : recording ? Copy("ui_e5c295ae8f") : savedCount > 0 ? Copy("ui_5ea1972f3b") : Copy("ui_0afad95194");
            nextQuestionLabel.text = Copy("ui_bc981983e7");
            answerRecording = recording; answerSaving = saving; answerSavedCount = savedCount;
            ApplyAnswerButtonGates();
            StyleButton(skipQuestion, nextQuestionLabel, skipQuestion.interactable);
            StyleButton(recordingPlaceholder, recordAnswerLabel, true);
            questionCount.text = Copy("ui_1e7962141e") + activeQuestion + Copy("ui_2a831953c3") + savedCount + (savedCount == 1 ? Copy("ui_0d3f865256") : Copy("ui_9cd6a3a01e"));
            int elapsed = Mathf.FloorToInt(seconds);
            string questionDetails = recordingQuestion + (string.IsNullOrEmpty(error) ? "" : "\n\nRecording details:\n" + error);
            if (body.text != questionDetails) { body.text = questionDetails; RefreshNarrative(); }
            status.text = !string.IsNullOrEmpty(error) ? Copy("ui_6b3906d082") : saving ? Copy("ui_3310475cd4") : recording
                ? Copy("ui_7ac240a840") + (elapsed / 60).ToString("00") + ":" + (elapsed % 60).ToString("00") + Copy("ui_8efd1fe1ba")
                : savedCount > 0 && answerUserSaved ? Copy("ui_368c428ad0") : savedCount > 0 ? Copy("ui_0998163f82") : Copy("ui_556ad3b5ba");
            footer.text = Copy("ui_f7ac755b89");
            if (questionVoiceoverPlaying) { recordAnswerLabel.text = Copy("ui_dc69cff444"); status.text = Copy("ui_f0a78f739a"); }
        }
        public void ShowThought(string text, UnityAction onClose) {
            thoughtText.text = text; thoughtCanvas.gameObject.SetActive(true); thoughtClose.onClick.RemoveAllListeners(); thoughtClose.onClick.AddListener(onClose);
        }
        public void SetTint(bool enabled) { tintCanvas.gameObject.SetActive(enabled); }
        public void SetProgress(float value) { progress.value = Mathf.Clamp01(value); }
        public void SetWristShortcut(string caption, UnityAction action) {
            var label = wristHelp.GetComponentInChildren<TMP_Text>(true); if (label) label.text = caption;
            UpdateInstructionButton();
            wristHelp.onClick.RemoveAllListeners(); if (action != null) wristHelp.onClick.AddListener(action);
        }
        void ShowFixedInstructions() { instructionsHiddenByUser = false; mainCanvas.gameObject.SetActive(true); UpdateInstructionButton(); Recenter(); }
        [ContextMenu("Reposition fixed panel near child")]
        public void RepositionFixedPanel() { fixedPanelPlaced = false; PlaceFixedPanelOnce(); }
        void PlaceFixedPanelOnce() {
            if (fixedPanelPlaced || !mainCanvas || (!fixedPanelAnchor && (!panelReferenceChild || !head))) return;
            if (fixedPanelAnchor) {
                fixedPanelPosition = fixedPanelAnchor.position; fixedPanelRotation = fixedPanelAnchor.rotation;
            } else {
                Vector3 forward = Vector3.ProjectOnPlane(panelReferenceChild.position - head.position, Vector3.up).normalized;
                if (forward.sqrMagnitude < .1f) forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                fixedPanelPosition = panelReferenceChild.position + right * fixedPanelOffset.x + forward * fixedPanelOffset.z + Vector3.up * fixedPanelOffset.y;
                Vector3 away = Vector3.ProjectOnPlane(fixedPanelPosition - head.position, Vector3.up);
                if (away.sqrMagnitude < .001f) away = forward;
                fixedPanelRotation = Quaternion.LookRotation(away.normalized, Vector3.up);
            }
            mainCanvas.transform.localScale = Vector3.one * fixedPanelScale;
            mainCanvas.transform.SetPositionAndRotation(fixedPanelPosition, fixedPanelRotation);
            fixedPanelPlaced = true;
        }
        public void Recenter() {
            if (!followParent) { if (!useAuthoredCanvas) PlaceFixedPanelOnce(); return; }
            if (!head) return;
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude > .1f) panelForward = forward;
            if (sceneFocus) { Vector3 target = sceneFocus.position - head.position; target.y = 0;
                float angle = Vector3.SignedAngle(panelForward, target, Vector3.up); if (Mathf.Abs(angle) > 8) side = angle > 0 ? -1 : 1;
            }
        }
        // Exposed for the Editor comfort check; frame-rate-independent and never alters the headset.
        public void UpdatePanelPose(float deltaTime) {
            if (!mainCanvas || !mainCanvas.gameObject.activeSelf) return;
            if (!followParent) {
                if (useAuthoredCanvas) return; // Use the saved RectTransform exactly, like a normal world-space canvas.
                PlaceFixedPanelOnce();
                if (fixedPanelPlaced) mainCanvas.transform.SetPositionAndRotation(fixedPanelPosition, fixedPanelRotation);
                return;
            }
            if (!head) return;
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
        Quaternion WristFrameYaw => wristReferenceFrame ? Quaternion.Euler(0, wristReferenceFrame.eulerAngles.y, 0) : Quaternion.identity;

        public void CaptureWristPlacement() {
            if (wristPlacementCaptured || !wristCanvas || !leftHand) return;
            var player = leftHand.GetComponentInParent<BNG.BNGPlayerController>();
            var adapter = leftHand.GetComponentInParent<PlaygroundRigAdapter>();
            wristReferenceFrame = player && player.TrackingSpace ? player.TrackingSpace : adapter ? adapter.playerRoot : null;
            stableWristOffset = Quaternion.Inverse(WristFrameYaw) * (wristCanvas.transform.position - leftHand.position);
            wristPlacementCaptured = true;
        }

        public void UpdateWristPose(float deltaTime) {
            if (!wristCanvas || !head) return;
            if (stabilizeWristAboveHand) {
                CaptureWristPlacement();
                if (wristPlacementCaptured && leftHand)
                    wristCanvas.transform.position = leftHand.position + WristFrameYaw * stableWristOffset;
            }
            Vector3 away = wristCanvas.transform.position - head.position;
            if (away.sqrMagnitude <= .001f) return;
            if (stabilizeWristAboveHand) {
                // World up prevents hand/head roll from tipping the canvas into the hand.
                var up = Mathf.Abs(Vector3.Dot(away.normalized, Vector3.up)) > .999f ? Vector3.forward : Vector3.up;
                wristCanvas.transform.rotation = Quaternion.LookRotation(away, up);
            } else wristCanvas.transform.rotation = Quaternion.Slerp(wristCanvas.transform.rotation, Quaternion.LookRotation(away, head.up), 1 - Mathf.Exp(-deltaTime * 14));
        }

        void OnEnable() { Application.onBeforeRender += StabilizeWristBeforeRender; }
        void OnDisable() { Application.onBeforeRender -= StabilizeWristBeforeRender; }
        void StabilizeWristBeforeRender() {
            if (Application.isPlaying && stabilizeWristAboveHand) UpdateWristPose(0);
        }

        void LateUpdate() {
            if (Application.isPlaying) { UpdateReplayAttention(Time.unscaledTime); UpdateInstructionButton(); }
            if (!Application.isPlaying || !head) return; UpdatePanelPose(Time.unscaledDeltaTime);
            UpdateWristPose(Time.unscaledDeltaTime);
            if (thoughtCanvas && thoughtCanvas.gameObject.activeSelf) { Vector3 away = thoughtCanvas.transform.position - head.position; away.y = 0; if (away.sqrMagnitude > .001f) thoughtCanvas.transform.rotation = Quaternion.Slerp(thoughtCanvas.transform.rotation, Quaternion.LookRotation(away, Vector3.up), 1 - Mathf.Exp(-Time.unscaledDeltaTime * 10)); }
        }
    }
}
