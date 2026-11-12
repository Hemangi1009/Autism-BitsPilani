using System;
using System.Collections;
using UnityEngine;

namespace BitsPilani.Module1.Playground {
    [Serializable]
    public sealed class PlaygroundVoiceoverCue {
        [Tooltip("Optional. None means this interaction has no new narration.")]
        public AudioClip clip;
        [Min(0), Tooltip("Seconds after the interaction before starting this clip. A pending clip is cancelled if the step changes or another assigned VO is triggered.")]
        public float delaySeconds;
    }

    public sealed class PlaygroundInteractionVoiceovers : MonoBehaviour {
        public AudioSource voice;
        public PlaygroundFlowController flow;
        public PlaygroundSessionLog session;

        [Header("Step 1 - exploration and gazes")]
        public PlaygroundVoiceoverCue explorationStarted = new PlaygroundVoiceoverCue();
        [Tooltip("When the child gaze target becomes available. Follows any preceding assigned narration.")]
        public PlaygroundVoiceoverCue childGazeStarted = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue childGazeCompleted = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue onlookerGazeStarted = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue onlookerGazeCompleted = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue partyGazeStarted = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue allIntroGazesCompleted = new PlaygroundVoiceoverCue();
        [Header("Step 2 - first observation")]
        public PlaygroundVoiceoverCue observationAreaShown = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue observationStarted = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue earlyWarningShown = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue firstBalloonPopped = new PlaygroundVoiceoverCue();
        [Header("Step 3 - clues")]
        public PlaygroundVoiceoverCue clueStepStarted = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue clueToolOpened = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue clueSelected = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue clueRemoved = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue cluePageChanged = new PlaygroundVoiceoverCue();
        [Header("Step 4 - guesses and confidence")]
        public PlaygroundVoiceoverCue guessStepStarted = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue guessToolOpened = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue guessSelected = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue confidenceSaved = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue guessPageChanged = new PlaygroundVoiceoverCue();
        [Header("Step 5 - approach, response and review")]
        public PlaygroundVoiceoverCue approachChildStarted = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue responseChoicesShown = new PlaygroundVoiceoverCue();
        [Header("Step 5 - optional selected-card dialogue")]
        public PlaygroundVoiceoverCue cardSpeakSoftly = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue cardEarDefenders = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue cardMoveAway = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue cardWaitAndWatch = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue cardLoudVoiceOrSuddenTouch = new PlaygroundVoiceoverCue();
        [Min(0), Tooltip("Pause after the selected card VO ends, or after the click if that VO is empty. Response Chosen plays next, including its own optional Delay Seconds.")]
        public float responseChosenPauseSeconds = 2.5f;
        public PlaygroundVoiceoverCue responseChosen = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue responseMidpoint = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue guessReviewStarted = new PlaygroundVoiceoverCue();
        [Header("Step 6 - child perspective")]
        public PlaygroundVoiceoverCue childPerspectiveOffered = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue childViewEntered = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue returnToParentReady = new PlaygroundVoiceoverCue();
        [Header("Step 7 - onlooker")]
        public PlaygroundVoiceoverCue onlookerStepStarted = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue thoughtGazeStarted = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue thoughtBubbleOpened = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue thoughtBubbleClosed = new PlaygroundVoiceoverCue();
        [Header("Step 8 - reflection (question clips also go into answer WAVs)")]
        public PlaygroundVoiceoverCue reflectionIntroduction = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue reflectionQuestion1 = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue reflectionQuestion2 = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue reflectionQuestion3 = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue answerSaved = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue reflectionFeedbackShown = new PlaygroundVoiceoverCue();
        [Header("Step 9 - one replay")]
        public PlaygroundVoiceoverCue replayOffered = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue replayStarted = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue replayObservationAreaShown = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue replayObservationStarted = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue replayEarlyWarningShown = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue replayBalloonPopped = new PlaygroundVoiceoverCue();
        public PlaygroundVoiceoverCue replayPreventionChosen = new PlaygroundVoiceoverCue();
        [Header("Summary")]
        public PlaygroundVoiceoverCue summaryShown = new PlaygroundVoiceoverCue();

        enum ResponseVoiceStage { None, Card, Pause, Chosen }
        ResponseVoiceStage responseVoiceStage;
        float responsePauseUntil;
        public bool IsResponseSequenceActive => responseVoiceStage != ResponseVoiceStage.None;

        AudioClip pendingClip, queuedStartClip;
        float queuedStartTime;
        bool pendingIsStart;
        float scheduledStartTime;
        int generation;
        public AudioClip PendingClip => pendingClip;
        public float ScheduledStartTime => scheduledStartTime;
        public AudioClip QueuedStartClip => queuedStartClip;
        public bool IsPlaying => IsResponseSequenceActive || pendingClip || queuedStartClip || voice && voice.isPlaying;

        public PlaygroundVoiceoverCue GetQuestionCue(int question) {
            return question == 1 ? reflectionQuestion1 : question == 2 ? reflectionQuestion2 : question == 3 ? reflectionQuestion3 : null;
        }

        public bool Play(PlaygroundVoiceoverCue cue) {
            // Step 5 midpoint/review narration must not cut off the requested card -> pause -> response sequence.
            return IsResponseSequenceActive ? ScheduleAfterCurrent(cue, Time.time) : Schedule(cue, Time.time);
        }

        public PlaygroundVoiceoverCue GetResponseCardCue(PlaygroundResponse response) {
            switch (response) {
                case PlaygroundResponse.SpeakSoftly: return cardSpeakSoftly;
                case PlaygroundResponse.EarDefenders: return cardEarDefenders;
                case PlaygroundResponse.MoveAway: return cardMoveAway;
                case PlaygroundResponse.WaitAndWatch: return cardWaitAndWatch;
                case PlaygroundResponse.LoudVoiceOrSuddenTouch: return cardLoudVoiceOrSuddenTouch;
                default: return null;
            }
        }

        public bool PlayResponseCardSequence(PlaygroundResponse response) { return ScheduleResponseCardSequence(response, Time.time); }

        public bool ScheduleResponseCardSequence(PlaygroundResponse response, float now) {
            if (!voice || !isActiveAndEnabled) return false;
            CancelPending();
            if (Application.isPlaying) voice.Stop();
            var cue = GetResponseCardCue(response);
            if (cue != null && cue.clip) {
                pendingClip = cue.clip;
                scheduledStartTime = now + Mathf.Max(0, cue.delaySeconds);
                responseVoiceStage = ResponseVoiceStage.Card;
            } else {
                responsePauseUntil = now + Mathf.Max(0, responseChosenPauseSeconds);
                responseVoiceStage = ResponseVoiceStage.Pause;
            }
            return true;
        }

        public bool Schedule(PlaygroundVoiceoverCue cue, float now) {
            // Empty cues do not interrupt an existing clip or fall back to legacy narration.
            if (cue == null || !cue.clip || !voice || !isActiveAndEnabled) return false;
            CancelPending();
            if (Application.isPlaying) voice.Stop();
            pendingClip = cue.clip;
            scheduledStartTime = now + Mathf.Max(0, cue.delaySeconds);
            return true;
        }

        // Start cues can occur in the same frame as the previous action's completion cue.
        // Preserve that completion VO, then announce the new interaction without overlap.
        public bool PlayAfterCurrent(PlaygroundVoiceoverCue cue) { return ScheduleAfterCurrent(cue, Time.time); }

        public bool ScheduleAfterCurrent(PlaygroundVoiceoverCue cue, float now) {
            if (cue == null || !cue.clip || !voice || !isActiveAndEnabled) return false;
            if (!IsPlaying) {
                bool scheduled = Schedule(cue, now);
                pendingIsStart = scheduled;
                return scheduled;
            }
            queuedStartClip = cue.clip;
            queuedStartTime = now + Mathf.Max(0, cue.delaySeconds);
            return true;
        }

        public IEnumerator PlayAndWait(PlaygroundVoiceoverCue cue) {
            if (!Play(cue)) yield break;
            int token = generation;
            while (token == generation && IsPlaying) yield return null;
        }

        public void CancelPending() {
            if (IsResponseSequenceActive && voice && Application.isPlaying) voice.Stop();
            responseVoiceStage = ResponseVoiceStage.None;
            pendingClip = null; queuedStartClip = null; pendingIsStart = false; generation++;
        }

        public void CancelWaitingStart() {
            queuedStartClip = null;
            if (pendingIsStart) { pendingClip = null; pendingIsStart = false; generation++; }
        }

        public bool AdvancePlayback(float now) {
            if (!voice) return false;
            // Observe real playback completion rather than guessing from clip length/pitch.
            if (responseVoiceStage == ResponseVoiceStage.Card && !pendingClip && !voice.isPlaying) {
                responsePauseUntil = now + Mathf.Max(0, responseChosenPauseSeconds);
                responseVoiceStage = ResponseVoiceStage.Pause;
            }
            if (responseVoiceStage == ResponseVoiceStage.Pause && now >= responsePauseUntil) {
                if (responseChosen != null && responseChosen.clip) {
                    pendingClip = responseChosen.clip;
                    scheduledStartTime = now + Mathf.Max(0, responseChosen.delaySeconds);
                    responseVoiceStage = ResponseVoiceStage.Chosen;
                } else responseVoiceStage = ResponseVoiceStage.None;
            }
            if (responseVoiceStage == ResponseVoiceStage.Chosen && !pendingClip && !voice.isPlaying)
                responseVoiceStage = ResponseVoiceStage.None;

            if (!IsResponseSequenceActive && !pendingClip && queuedStartClip && !voice.isPlaying) {
                pendingClip = queuedStartClip;
                queuedStartClip = null;
                pendingIsStart = true;
                scheduledStartTime = queuedStartTime;
            }
            if (!pendingClip || now < scheduledStartTime) return false;
            var clip = pendingClip;
            pendingIsStart = false;
            pendingClip = null;
            voice.clip = clip;
            if (Application.isPlaying) voice.Play();
            if (session && session.Data != null)
                session.Record(flow ? flow.CurrentStep.ToString() : "Audio", "Voiceover", clip.name);
            return true;
        }

        void Update() { AdvancePlayback(Time.time); }
        void OnDisable() { CancelPending(); if (voice && Application.isPlaying) voice.Stop(); }
    }
}
