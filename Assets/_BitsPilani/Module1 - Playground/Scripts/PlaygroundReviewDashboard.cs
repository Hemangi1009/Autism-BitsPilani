using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BitsPilani.Module1.Playground {
    [Serializable]
    public sealed class PlaygroundReviewListRow {
        public RectTransform root;
        public TMP_Text value, detail;
        public LayoutElement layout;
        [Min(1)] public float minimumHeight = 60;
    }

    /// <summary>
    /// Populates scene-authored review lists and tables. Never creates UI or writes colours/fonts.
    /// Normal flow buttons, recording requirements, replay count and VO remain unchanged.
    /// </summary>
    public sealed class PlaygroundReviewDashboard : MonoBehaviour {
        [Header("Module references")]
        public PlaygroundFlowController flow;
        public GameObject narrativeRoot, statusRoot, statusBackgroundRoot, videoStatusRoot;

        [Header("Clues you selected")]
        public ScrollRect cluesScroll;
        public TMP_Text clueCount, noClues;
        public PlaygroundReviewListRow[] clueRows;

        [Header("Your guesses and confidence")]
        public ScrollRect guessesScroll;
        public TMP_Text guessCount, noGuesses;
        public PlaygroundReviewListRow[] guessRows;

        [Header("First-attempt response table")]
        public GameObject firstAttemptTable;
        public TMP_Text firstAction, firstOutcome, firstTiming;

        [Header("First attempt / replay comparison table")]
        public GameObject comparisonTable;
        public TMP_Text compareFirstAction, compareReplayAction;
        public TMP_Text compareFirstOutcome, compareReplayOutcome;
        public TMP_Text compareFirstTiming, compareReplayTiming;

        [Header("Editable wording")]
        public string reflectionHeading = "Review your first attempt";
        public string comparisonHeading = "First attempt and replay";
        public string reflectionPrompt = "How do your clues, guesses and response fit together?";
        public string comparisonPrompt = "What did you change during the replay?";
        public string scrollPrompt = "Scroll within the lists to see every selection.";
        public string guessReviewInstruction = "Choose a guess on the right to update your confidence.";
        public string contextCluesHeading = "Clues you selected";
        public string contextActionHeading = "Your action";
        public string contextOutcomeHeading = "Child's response";
        public string confidencePrefix = "Confidence: ";
        public string clueCountFormat = "{0} selected";
        public string guessCountFormat = "{0} saved";
        public string recordingsFormat = "Answers recorded: {0}/3   |   Clips saved: {1}";
        public string notRecorded = "No response recorded";
        public string replayNotAttempted = "Not attempted";
        public string replaySkipped = "Replay skipped";
        public string afterPopFormat = "{0:0.0} s after the balloon pop";
        public string beforePopFormat = "{0:0.0} s before the balloon pop";
        public string afterPop = "After the balloon pop";

        public bool IsPresented { get; private set; }
        public bool HasOverflow { get; private set; }
        bool narrativeWasActive, statusWasActive, statusBackgroundWasActive, videoStatusWasActive, bound;

        public void Present(PlaygroundSessionData data, bool compare, string emptyClues, string emptyGuesses) {
            if (data == null) { Hide(); return; }
            if (!IsPresented) {
                narrativeWasActive = narrativeRoot && narrativeRoot.activeSelf;
                statusWasActive = statusRoot && statusRoot.activeSelf;
                statusBackgroundWasActive = statusBackgroundRoot && statusBackgroundRoot.activeSelf;
                videoStatusWasActive = videoStatusRoot && videoStatusRoot.activeSelf;
            }
            IsPresented = true;
            gameObject.SetActive(true);
            if (narrativeRoot) narrativeRoot.SetActive(false);
            if (statusRoot) statusRoot.SetActive(false);
            if (statusBackgroundRoot) statusBackgroundRoot.SetActive(false);
            if (videoStatusRoot) videoStatusRoot.SetActive(false);
            BindScrollbars();

            int clues = data.clues == null ? 0 : data.clues.Count;
            int guesses = data.guesses == null ? 0 : data.guesses.Count;
            clueCount.text = string.Format(clueCountFormat, clues);
            guessCount.text = string.Format(guessCountFormat, guesses);
            noClues.text = emptyClues; noClues.gameObject.SetActive(clues == 0);
            noGuesses.text = emptyGuesses; noGuesses.gameObject.SetActive(guesses == 0);
            FillList(clueRows, cluesScroll, clues, i => (i + 1) + ". " + data.clues[i], null);
            FillList(guessRows, guessesScroll, guesses, i => (i + 1) + ". " + data.guesses[i].idea,
                i => confidencePrefix + Confidence(data.guesses[i]));

            firstAttemptTable.SetActive(!compare);
            comparisonTable.SetActive(compare);
            firstAction.text = compareFirstAction.text = data.firstResponse;
            firstOutcome.text = compareFirstOutcome.text = data.firstOutcome;
            firstTiming.text = compareFirstTiming.text = data.firstResponseSeconds < 0
                ? notRecorded : string.Format(afterPopFormat, data.firstResponseSeconds);
            compareReplayAction.text = data.replayResponse;
            compareReplayOutcome.text = data.replayOutcome;
            compareReplayTiming.text = ReplayTiming(data);
            Canvas.ForceUpdateCanvases();
            HasOverflow = RefreshScroll(cluesScroll) | RefreshScroll(guessesScroll);
        }

        TMP_Text contextText;
        FontStyles contextStyle;
        FontWeight contextWeight;

        // Only the response-review paragraph uses regular detail text and bold headings.
        // Restore the authored style when the next UI page is shown.
        public void FormatGuessReviewText(TMP_Text text) {
            if (!text) return;
            if (!contextText) { contextText = text; contextStyle = text.fontStyle; contextWeight = text.fontWeight; }
            text.fontStyle &= ~FontStyles.Bold;
            text.fontWeight = FontWeight.Regular;
            text.ForceMeshUpdate();
        }

        public void Hide() {
            if (contextText) {
                contextText.fontStyle = contextStyle;
                contextText.fontWeight = contextWeight;
                contextText = null;
            }
            if (IsPresented) {
                if (narrativeRoot) narrativeRoot.SetActive(narrativeWasActive);
                if (statusRoot) statusRoot.SetActive(statusWasActive);
                if (statusBackgroundRoot) statusBackgroundRoot.SetActive(statusBackgroundWasActive);
                if (videoStatusRoot) videoStatusRoot.SetActive(videoStatusWasActive);
            }
            IsPresented = false;
            gameObject.SetActive(false);
        }

        string Confidence(PlaygroundGuess guess) {
            return flow && flow.Confidence != null && guess.confidence >= 0 && guess.confidence < flow.Confidence.Length
                ? flow.Confidence[guess.confidence] : guess.ConfidenceLabel;
        }

        string ReplayTiming(PlaygroundSessionData data) {
            if (data.skippedSteps != null && data.skippedSteps.Contains(PlaygroundStep.ReplayAndPrevent.ToString())) return replaySkipped;
            if (data.replayCount == 0) return replayNotAttempted;
            if (string.IsNullOrEmpty(data.replayResponse) || data.replayResponse == "No response") return notRecorded;
            return data.replaySecondsBeforePop > 0 ? string.Format(beforePopFormat, data.replaySecondsBeforePop) : afterPop;
        }

        public string RecordingSummary(PlaygroundSessionData data) {
            int questions = 0;
            for (int q = 1; q <= 3; q++)
                if (data.reflectionClips != null && data.reflectionClips.Exists(c => c.question == q)) questions++;
            return string.Format(recordingsFormat, questions, data.reflectionClips == null ? 0 : data.reflectionClips.Count);
        }

        public string GuessReviewContext(PlaygroundSessionData data, string emptyClues) {
            var text = new StringBuilder("<b>" + contextCluesHeading + "</b>\n");
            if (data.clues == null || data.clues.Count == 0) text.AppendLine(emptyClues);
            else for (int i = 0; i < data.clues.Count; i++) text.AppendLine((i + 1) + ". " + data.clues[i]);
            text.Append("\n<b>").Append(contextActionHeading).Append("</b>\n").AppendLine(data.firstResponse);
            text.Append("\n<b>").Append(contextOutcomeHeading).Append("</b>\n").Append(data.firstOutcome);
            return text.ToString();
        }

        void BindScrollbars() {
            if (bound) return;
            bound = true;
            foreach (var scroll in new[] { cluesScroll, guessesScroll }) {
                var captured = scroll;
                if (captured && captured.verticalScrollbar)
                    captured.verticalScrollbar.onValueChanged.AddListener(v => captured.verticalNormalizedPosition = v);
            }
        }

        void FillList(PlaygroundReviewListRow[] rows, ScrollRect scroll, int count, Func<int, string> value, Func<int, string> detail) {
            if (rows == null || !scroll) return;
            for (int i = 0; i < rows.Length; i++) {
                bool visible = i < count;
                rows[i].root.gameObject.SetActive(visible);
                if (!visible) continue;
                rows[i].value.text = value(i);
                if (rows[i].detail) rows[i].detail.text = detail == null ? "" : detail(i);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            for (int i = 0; i < Mathf.Min(count, rows.Length); i++) {
                var row = rows[i];
                float height = row.value.GetPreferredValues(row.value.text, Mathf.Max(1, row.value.rectTransform.rect.width), Mathf.Infinity).y + 12;
                if (row.detail) height += row.detail.GetPreferredValues(row.detail.text, Mathf.Max(1, row.detail.rectTransform.rect.width), Mathf.Infinity).y + 6;
                row.layout.preferredHeight = Mathf.Max(row.minimumHeight, height);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.verticalNormalizedPosition = 1;
        }

        bool RefreshScroll(ScrollRect scroll) {
            if (!scroll || !scroll.viewport || !scroll.content) return false;
            bool overflow = scroll.content.rect.height > scroll.viewport.rect.height + 1;
            scroll.vertical = overflow;
            if (scroll.verticalScrollbar) {
                scroll.verticalScrollbar.gameObject.SetActive(overflow);
                // Keep the track invisible/non-interactive if native ScrollRect layout refreshes its active state.
                var visibility = scroll.verticalScrollbar.GetComponent<CanvasGroup>();
                if (visibility) { visibility.alpha = overflow ? 1 : 0; visibility.interactable = overflow; visibility.blocksRaycasts = overflow; }
                scroll.verticalScrollbar.size = Mathf.Clamp01(scroll.viewport.rect.height / Mathf.Max(1, scroll.content.rect.height));
            }
            return overflow;
        }
    }
}
