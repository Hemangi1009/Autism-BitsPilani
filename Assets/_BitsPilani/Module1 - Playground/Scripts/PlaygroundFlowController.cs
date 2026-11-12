using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.VFX;

namespace BitsPilani.Module1.Playground {
    public enum PlaygroundStep { Loading, EnterPlayground, WatchChanges, CollectClues, NoteGuesses, TestGuesses, ChildPerspective, OnlookerThought, Reflect, ReplayAndPrevent, Summary, Complete }
    public enum PlaygroundResponse { SpeakSoftly, EarDefenders, MoveAway, WaitAndWatch, LoudVoiceOrSuddenTouch }

    public sealed class PlaygroundFlowController : MonoBehaviour {
        [Header("Module services")]
        public PlaygroundModuleUI ui;
        public PlaygroundRigAdapter rig;
        public PlaygroundSessionLog session;
        public PlaygroundCharacterPose child;
        [Header("Perspective bodies and session video")]
        public PlaygroundPerspectiveBodies perspectiveBodies;
        public PlaygroundSessionRecording recording;
        [Header("Step 8 microphone answers")]
        public PlaygroundAnswerRecorder answerRecorder;
        [Header("Story locations")]
        public Transform gazeChild, gazeOnlooker, gazeParty, gazeThought;
        public Transform observationPoint, approachPoint, quietPoint, childViewPoint, partyFocus;
        public GameObject observationCircle, approachCircle;
        public float arrivalRadius = .8f;
        [Header("Audio and narration")]
        public AudioSource voice, partyMusic, crowd, pop;
        [Header("Optional VO per interaction - edit the linked component" )]
        public PlaygroundInteractionVoiceovers interactionVoiceovers;
        [HideInInspector] public AudioClip[] stepVoiceovers = new AudioClip[10];
        [HideInInspector] public AudioClip changeVoice, responseVoice, replayHintVoice, summaryVoice;
        [Header("Reflection question voiceovers")]
        [Tooltip("Question 1, 2 and 3: played in the scene and prepended to every saved answer take.")]
        [HideInInspector] public AudioClip[] reflectionQuestionVoiceovers = new AudioClip[3];
        public AudioClip GetReflectionQuestionVoiceover(int question) {
            var cue = interactionVoiceovers ? interactionVoiceovers.GetQuestionCue(question) : null;
            return cue == null ? null : cue.clip;
        }
        bool questionNarrating;
        readonly bool[] answerSavedByUser = new bool[3];
        Coroutine questionVoiceRoutine;
        [Header("Balloon pop - optional future VFX")]
        public GameObject eventBalloon, balloonString;
        public Transform popOrigin;
        public VisualEffect balloonPopVfx;
        public GameObject balloonPopVfxPrefab;
        public UnityEvent onBalloonPopped = new UnityEvent();
        public UnityEvent onModuleCompleted = new UnityEvent();
        [Header("Child-perspective props")]
        public Transform[] perspectiveProps;
        [Header("Durations in seconds")]
        [Min(6)] public float initialLoadSeconds = 6f;
        [Min(1), Tooltip("Total first-observation time. Warning occurs at 16%, balloon pop at 40%.")] public float watchSeconds = 25f;
        [Min(1), Tooltip("Time to observe the selected response. The child reaction changes halfway through.")] public float responseSeconds = 20f;
        [Min(1), Tooltip("Child-view observation time before Return becomes available.")] public float perspectiveSeconds = 25f;
        [HideInInspector] public float thoughtToReflectionSeconds = 4f; // Legacy scene value; no delay is used.
        [Min(1), Tooltip("Replay time before the pop. Early warning occurs at 20% of this duration.")] public float replayWarningSeconds = 15f;
        [Min(0)] public float replayAfterPopSeconds = 8f;

        // Keep the authored default pacing while scaling story events with each phase.
        public float WatchWarningTime => watchSeconds * .16f;
        public float WatchPopTime => watchSeconds * .4f;
        public float ResponseMidpointTime => responseSeconds * .5f;
        public float ReplayEarlyWarningTime => replayWarningSeconds * .2f;

        public PlaygroundStep CurrentStep { get; private set; }
        public bool IntroGazesComplete { get; private set; }
        public bool ResponseReady => responseReady;
        public bool ReviewingGuesses => reviewingGuesses;
        public bool ReturnViewReady => returnReady;
        public bool ThoughtOpen => thoughtOpen;
        public bool ReflectionFeedbackShown => reflectionFeedback;
        public bool ReplayStarted => replayStarted;
        public bool ReplayAreaReached => replayAreaReached;
        public bool ReplayPopOccurred => replayPop;
        public int ReflectionQuestion => reflectionIndex;
        public int SelectedClueCount => session.Data == null ? 0 : session.Data.clues.Count;
        public int GuessCount => session.Data == null ? 0 : session.Data.guesses.Count;

        public string[] Clues = {
            "Hands covering his ears", "His attention stays on the birthday cake",
            "His body has become tense", "He is pointing toward the slide entrance",
            "He is pulling at his clothes", "Nearby crowd and voices",
            "His hands are resting by his sides", "Looking toward the speaker",
            "Loud party speaker", "He is reaching for a balloon",
            "Balloons near the slide", "He is looking at another child's toy"
        };
        static readonly bool[] ClueObserved = { true, false, true, false, false, true, false, true, true, false, true, false };
        public string[] GuessCards = {
            "He finds the sound too loud", "He is frustrated about waiting for a turn",
            "He is scared of the balloons", "He is hungry and wants some cake",
            "The nearby crowd feels too close", "He is tired and needs a rest",
            "He is worried another balloon might pop", "He wants to leave this area",
            "He feels too warm in his clothes", "He wants to join the birthday party",
            "He is worried about going down the slide", "He wants another child's toy"
        };
        // Scenario evidence for facilitator logs only. Do not reveal an answer key on the cards.
        // A plausible hypothesis is not a diagnosis or an established fact about the child.
        public string[] GuessEvidence = {
            "Supported by sound-linked cues", "Not supported by the shown cues",
            "Plausible; needs checking", "Not supported by the shown cues",
            "Plausible; needs checking", "Not supported by the shown cues",
            "Supported by the sudden-sound event", "Plausible; needs checking",
            "Not supported by the shown cues", "Not supported by the shown cues",
            "Not supported by the shown cues", "Not supported by the shown cues"
        };
        public string[] Confidence = { "Not sure", "Somewhat sure", "Very sure" };
        public string[] ResponseCards = { "Speak softly and ask gently", "Offer ear defenders", "Move him away from the speaker", "Wait quietly and watch", "Use a loud voice or sudden touch" };
        public string[] ReplayCards = {
            "Offer ear defenders before the sound builds",
            "Invite him closer to the cake to distract him",
            "Move together to a quieter spot",
            "Keep his place at the slide and wait nearby",
            "Ask him to try one more turn before leaving",
            "Offer a balloon to hold while the party continues"
        };
        public static bool ReplayChoiceReducesSound(int index) { return index == 0 || index == 2; }
        public string[] ReflectionQuestions = { "What did you first think was happening?", "What changed your mind?", "Is something wrong with him?" };
        public string[] StepCaptions = { "Getting ready", "Explore", "Watch what changes", "Collect clues", "Note guesses", "Test guesses", "Child view", "Onlooker thought", "Reflect", "Replay and prevent", "Summary", "Complete" };
        readonly List<GameObject> spawnedPopEffects = new List<GameObject>();
        Vector3[] propPositions, propScales;
        Quaternion[] propRotations;
        bool responseReady, reviewingGuesses, returnReady, thoughtOpen, reflectionFeedback;
        bool thoughtViewedOnce;
        public bool ThoughtViewedOnce => thoughtViewedOnce;
        bool replayStarted, replayAreaReached, replayPop, replayChoiceMade, firstPop;
        bool responding, replaySupportChosen;
        int introCount, reflectionIndex, gazeToken;
        float firstPopAt, replayStartedAt;
        int pendingGuess = -1;
        int cluePage, guessPage;
        Coroutine routine;
        bool initialized;
        bool completedEventSent;
        bool skippingStep;
        int lastSkipFrame = -1;
        public bool CanSkipCurrentStep => session && session.Data != null && !skippingStep && !finishingModule &&
            CurrentStep >= PlaygroundStep.EnterPlayground && CurrentStep <= PlaygroundStep.ReplayAndPrevent &&
            (!answerRecorder || !answerRecorder.IsSaving);
        bool retryRecordingRequested, finishingModule;
        int lastAnswerUiSecond = -1;

        void Start() {
            if (!ValidateReferences()) { enabled = false; return; }
            InitializeAnswerRecording();
            initialized = true; session.BeginSession(); ui.sessionVideoStatus = recording ? recording.recordingStatusText : null; ui.panelReferenceChild = child.transform; ui.Initialize(); ui.sceneFocus = child.transform; child.CaptureRestPose();
            propPositions = new Vector3[perspectiveProps.Length]; propScales = new Vector3[perspectiveProps.Length]; propRotations = new Quaternion[perspectiveProps.Length];
            for (int i = 0; i < perspectiveProps.Length; i++) { propPositions[i] = perspectiveProps[i].localPosition; propScales[i] = perspectiveProps[i].localScale; propRotations[i] = perspectiveProps[i].localRotation; }
            SetStandAreaVisible(observationCircle, false); SetStandAreaVisible(approachCircle, false);
            partyMusic.volume = .10f; crowd.volume = .04f;
            if (balloonPopVfx) balloonPopVfx.Stop();
            partyMusic.Play(); crowd.Play();
            routine = StartCoroutine(LoadThenEnter());
        }

        bool ValidateReferences() {
            bool valid = ui && rig && session && child && voice && partyMusic && crowd && pop && eventBalloon && popOrigin &&
                         gazeChild && gazeOnlooker && gazeParty && gazeThought && observationPoint && approachPoint && quietPoint && childViewPoint && partyFocus && observationCircle && approachCircle;
            if (!valid) Debug.LogError("[M1 Playground][Configuration] Required flow references are missing. Use the module scene builder.", this);
            if (valid && (!FactoryManager.Instance || !Pointer.instance)) {
                Debug.LogError("[M1 Playground][Configuration] The MySample FactoryManager and gaze Pointer must be present in the scene.", this); valid = false;
            }
            return valid;
        }

        void SetStep(PlaygroundStep next) {
            if (interactionVoiceovers) interactionVoiceovers.CancelPending(); CurrentStep = next; ui.sceneFocus = next == PlaygroundStep.OnlookerThought ? gazeOnlooker : next == PlaygroundStep.ChildPerspective ? partyFocus : child.transform; ui.SetStep((int)next, StepCaptions[(int)next]); ui.SetProgress(0);
            ui.wristStatus.text = StepCaptions[(int)next];
            ui.SetWristTools(false, false, null, null);
            session.Record(next.ToString(), "StepEntered"); RefreshSkipButton();
        }
        void RefreshSkipButton() { if (ui && ui.skipStep) ui.skipStep.interactable = CanSkipCurrentStep; }

        public void SkipCurrentStep() {
            if (!CanSkipCurrentStep || lastSkipFrame == Time.frameCount) return;
            lastSkipFrame = Time.frameCount;
            skippingStep = true; RefreshSkipButton();
            var skipped = CurrentStep;
            try {
                gazeToken++;
                StopAllCoroutines(); routine = null; questionVoiceRoutine = null; questionNarrating = false;
                if (answerRecorder && answerRecorder.IsRecording) answerRecorder.StopAndSave("Step skipped");
                if (interactionVoiceovers) interactionVoiceovers.CancelPending();
                if (voice) voice.Stop();
                foreach (var anchor in new[] { gazeChild, gazeOnlooker, gazeParty, gazeThought }) if (anchor) ClearGazeChildren(anchor);
                SetStandAreaVisible(observationCircle, false); SetStandAreaVisible(approachCircle, false);
                if (rig.InChildView) {
                    rig.ReturnToParentView();
                    if (perspectiveBodies) perspectiveBodies.RestoreParentView();
                    RestorePerspectiveProps(); partyMusic.pitch = 1; partyMusic.volume = .18f; crowd.volume = .09f;
                }
                rig.SetMovementLocked(false); ui.SetTint(false); ui.SetChildViewReturn(false, false, null);
                ui.thoughtCanvas.gameObject.SetActive(false); ui.HideReplayAttention();
                pendingGuess = -1; responding = false; reviewingGuesses = false; responseReady = false;
                if (!session.Data.skippedSteps.Contains(skipped.ToString())) session.Data.skippedSteps.Add(skipped.ToString());
                string detail = "Whole step skipped";
                if (skipped == PlaygroundStep.CollectClues) { session.Data.clues.Clear(); detail = ui.noCluesSelectedText; }
                if (skipped == PlaygroundStep.NoteGuesses) { session.Data.guesses.Clear(); detail = ui.noGuessesSelectedText; }
                session.Record(skipped.ToString(), "StepSkipped", detail);
                switch (skipped) {
                    case PlaygroundStep.EnterPlayground:
                        IntroGazesComplete = true; introCount = 3; routine = StartCoroutine(WatchChanges()); break;
                    case PlaygroundStep.WatchChanges:
                        firstPop = true; firstPopAt = Time.time; SetBalloonVisible(false);
                        child.SetState(PlaygroundChildState.Distressed); BeginClues(); break;
                    case PlaygroundStep.CollectClues:
                        BeginGuesses(); ui.footer.text = ui.noCluesSelectedText; break;
                    case PlaygroundStep.NoteGuesses:
                        BeginResponseStep(); ui.footer.text = ui.noGuessesSelectedText; break;
                    case PlaygroundStep.TestGuesses:
                        session.Data.firstOutcome = "Not observed (step skipped)";
                        BeginChildPerspectiveStep(); break;
                    case PlaygroundStep.ChildPerspective: BeginOnlooker(); break;
                    case PlaygroundStep.OnlookerThought: BeginReflection(); break;
                    case PlaygroundStep.Reflect:
                        for (int i = 0; i < session.Data.reflectionAnswers.Length; i++)
                            if (string.IsNullOrEmpty(session.Data.reflectionAnswers[i])) session.Data.reflectionAnswers[i] = "Not answered (step skipped)";
                        reflectionFeedback = true; PrepareReplay(); break;
                    case PlaygroundStep.ReplayAndPrevent:
                        session.Data.replayOutcome = "Not observed (step skipped)"; ShowSummary(); break;
                }
            } finally { skippingStep = false; RefreshSkipButton(); }
        }

        void PlayInteraction(PlaygroundVoiceoverCue cue) { if (interactionVoiceovers) interactionVoiceovers.Play(cue); }
        void PlayStartInteraction(PlaygroundVoiceoverCue cue) { if (interactionVoiceovers) interactionVoiceovers.PlayAfterCurrent(cue); }
        bool NarrationPlaying => interactionVoiceovers ? interactionVoiceovers.IsPlaying : voice && voice.isPlaying;
        void SpeakStep(int number) {
            if (!interactionVoiceovers) return;
            switch (number) {
                case 1: PlayInteraction(interactionVoiceovers.explorationStarted); break;
                case 2: PlayInteraction(interactionVoiceovers.observationStarted); break;
                case 3: PlayInteraction(interactionVoiceovers.clueStepStarted); break;
                case 4: PlayInteraction(interactionVoiceovers.guessStepStarted); break;
                case 5: PlayInteraction(interactionVoiceovers.approachChildStarted); break;
                case 6: PlayInteraction(interactionVoiceovers.childViewEntered); break;
                case 7: PlayInteraction(interactionVoiceovers.onlookerStepStarted); break;
                case 8: PlayInteraction(interactionVoiceovers.reflectionIntroduction); break;
                case 9: PlayInteraction(interactionVoiceovers.replayOffered); break;
            }
        }

        IEnumerator LoadThenEnter() {
            SetStep(PlaygroundStep.Loading); rig.SetMovementLocked(true);
            if (recording && recording.recordModule) {
                if (recording.participantIdPanel) recording.participantIdPanel.SetActive(true);
                if (!recording.IsParticipantIdReady) {
                    ui.mainCanvas.gameObject.SetActive(false);
                    while (!recording.IsParticipantIdReady) yield return null;
                }
                if (recording.participantIdPanel && recording.IsParticipantIdReady) recording.participantIdPanel.SetActive(false);
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            ui.Show(ui.Copy("flow_485d452835"), ui.Copy("flow_6c7b24d240"), ui.Copy("flow_db3f30c616"));
#endif
            // Request before capture: an Android permission dialog can pause the app.
            yield return PlaygroundMicrophonePermission.RequestAtModuleStart();
            if (recording && recording.recordModule) {
                bool ready = false;
                while (!ready) {
                    ui.Show(ui.Copy("flow_e552659151"), ui.Copy("flow_943bef98ea"), ui.Copy("flow_60ae10ee3a"));
                    var startRecording = recording.StartSessionAsync();
                    while (!startRecording.IsCompleted) yield return null;
                    ready = startRecording.Status == System.Threading.Tasks.TaskStatus.RanToCompletion && startRecording.Result;
                    if (startRecording.IsFaulted) Debug.LogError("[ModuleStart] Recording task failed: " + startRecording.Exception);
                    if (ready || recording.allowModuleWithoutRecording) break;
                    retryRecordingRequested = false;
                    ui.Show(ui.Copy("flow_8128f9719f"), recording.LastError, ui.Copy("flow_232904a09e"));
                    ui.SetPrimary(ui.Copy("flow_6897fbf60f"), () => retryRecordingRequested = true);
                    ui.SetSecondary(ui.Copy("flow_7ab65d262f"), ReturnToEntryAfterRecordingError);
                    while (!retryRecordingRequested) yield return null;
                }
            }
            ui.Show(ui.Copy("flow_4778be5184"), ui.Copy("flow_a15eafdaa0"), ui.Copy("flow_5ca8ed5a24"));
            ui.wristStatus.text = ui.Copy("flow_e3a0b78a91");
            float elapsed = 0; float wait = Mathf.Max(6f, initialLoadSeconds);
            while (elapsed < wait) { elapsed += Time.deltaTime; ui.SetProgress(elapsed / wait); yield return null; }
            rig.SetMovementLocked(false); BeginIntro();
        }

        async void ReturnToEntryAfterRecordingError() {
            var manager = FolderManager.Instance ? FolderManager.Instance : recording.folderManager;
            if (manager) await manager.ReturnToEntryAsync(recording);
        }

        void BeginIntro() {
            SetStep(PlaygroundStep.EnterPlayground); introCount = 0; IntroGazesComplete = false;
            child.ResetChild(); SetBalloonVisible(true);
            ui.Show(ui.Copy("flow_290a8917dd"), ui.Copy("flow_a572da7039"), ui.Copy("flow_2b5ca12371"));
            ui.hint.text = ui.Copy("flow_61d459f377");
            ui.wristStatus.text = ui.Copy("flow_c4ca270d10"); SpeakStep(1);
            int token = ++gazeToken;
            // Same FactoryManager Action callback chain as MySample; the package removes completed gaze prefabs.
            PlayStartInteraction(interactionVoiceovers?.childGazeStarted);
            FactoryManager.CreateGaze(gazeChild, () => {
                if (token != gazeToken || CurrentStep != PlaygroundStep.EnterPlayground || introCount != 0) return;
                if (interactionVoiceovers) interactionVoiceovers.CancelWaitingStart(); PlayInteraction(interactionVoiceovers?.childGazeCompleted); introCount = 1; session.Record("EnterPlayground", "GazeCompleted", "Child"); ui.wristStatus.text = ui.Copy("flow_2f7c8a4edd"); ui.status.text = ui.Copy("flow_4c4c8adfc2");
                PlayStartInteraction(interactionVoiceovers?.onlookerGazeStarted);
                FactoryManager.CreateGaze(gazeOnlooker, () => {
                    if (token != gazeToken || CurrentStep != PlaygroundStep.EnterPlayground || introCount != 1) return;
                    if (interactionVoiceovers) interactionVoiceovers.CancelWaitingStart(); PlayInteraction(interactionVoiceovers?.onlookerGazeCompleted); introCount = 2; session.Record("EnterPlayground", "GazeCompleted", "Onlooker"); ui.wristStatus.text = ui.Copy("flow_86215505d5"); ui.status.text = ui.Copy("flow_13cfe8c471");
                    PlayStartInteraction(interactionVoiceovers?.partyGazeStarted);
                    FactoryManager.CreateGaze(gazeParty, () => {
                        if (token != gazeToken || CurrentStep != PlaygroundStep.EnterPlayground || introCount != 2) return;
                        if (interactionVoiceovers) interactionVoiceovers.CancelWaitingStart(); PlayInteraction(interactionVoiceovers?.allIntroGazesCompleted); introCount = 3; IntroGazesComplete = true; SetStandAreaVisible(observationCircle, true);
                        ui.status.text = ui.Copy("flow_0d50ac8938"); ui.wristStatus.text = ui.Copy("flow_6b3a74d07a");
                        session.Record("EnterPlayground", "GazeCompleted", "Party; observation circle enabled");
                    });
                });
            });
        }

        void Update() {
            if (!initialized) return;
            if (CurrentStep == PlaygroundStep.Reflect && !reflectionFeedback && answerRecorder && answerRecorder.IsRecording) {
                int second = Mathf.FloorToInt(answerRecorder.RecordedSeconds);
                if (second != lastAnswerUiSecond) { lastAnswerUiSecond = second; RefreshAnswerRecordingUI(); }
            }
            // The scene-authored VFX define the destinations. They may be repositioned independently
            // of the legacy marker transforms when the scene layout is edited.
            if (CurrentStep == PlaygroundStep.EnterPlayground && IntroGazesComplete && StandAreaReached(observationCircle) && !NarrationPlaying) {
                session.Record("EnterPlayground", "ObservationCircleReached"); SetStandAreaVisible(observationCircle, false); routine = StartCoroutine(WatchChanges());
            }
            if (CurrentStep == PlaygroundStep.TestGuesses && !responseReady && !responding && !reviewingGuesses && StandAreaReached(approachCircle)) {
                responseReady = true; SetStandAreaVisible(approachCircle, false); session.Record("TestGuesses", "ChildApproached"); ShowResponses();
            }

            if (CurrentStep == PlaygroundStep.ReplayAndPrevent && replayStarted && !replayAreaReached && StandAreaReached(observationCircle)) {
                replayAreaReached = true; rig.SetMovementLocked(false); SetStandAreaVisible(observationCircle, false); session.Record("ReplayAndPrevent", "ReplayCircleReached", "Visible circle reached; movement remains enabled"); routine = StartCoroutine(RunReplay());
            }
        }

        void SetStandAreaVisible(GameObject area, bool visible) {
            var destination = area.GetComponent<PlaygroundStandArea>();
            if (destination) {
                if (visible) destination.Show(); else destination.Hide();
            } else area.SetActive(visible);
            if (visible && area == observationCircle)
                PlayStartInteraction(CurrentStep == PlaygroundStep.ReplayAndPrevent
                    ? interactionVoiceovers?.replayObservationAreaShown : interactionVoiceovers?.observationAreaShown);
        }

        bool StandAreaReached(GameObject area) {
            var destination = area.GetComponent<PlaygroundStandArea>();
            bool inside = rig.IsAt(area.transform, arrivalRadius);
            return destination ? destination.AdvanceArrival(Time.time, inside) : inside;
        }

        IEnumerator WatchChanges() {
            SetStep(PlaygroundStep.WatchChanges); rig.Face(child.transform); rig.SetMovementLocked(true);
            ui.Show(ui.Copy("flow_5a2efe1a31"), ui.Copy("flow_9b6a5e1503"), ui.Copy("flow_08e7fd60ed"));
            ui.wristStatus.text = ui.Copy("flow_32fadcf968"); SpeakStep(2);
            float start = Time.time; bool warned = false; firstPop = false;
            while (Time.time - start < watchSeconds) {
                float elapsed = Time.time - start;
                if (!warned && elapsed >= WatchWarningTime) { warned = true; crowd.volume = .12f; child.SetState(PlaygroundChildState.EarlyWarning); PlayInteraction(interactionVoiceovers?.earlyWarningShown); session.Record("WatchChanges", "EarlyCue", "Hand to ear; crowd louder; still pose"); }
                if (!firstPop && elapsed >= WatchPopTime) { firstPop = true; firstPopAt = Time.time; PopBalloon(); child.SetState(PlaygroundChildState.Distressed); PlayInteraction(interactionVoiceovers?.firstBalloonPopped); }
                ui.status.text = (firstPop ? ui.Copy("flow_02924b240f") : ui.Copy("flow_5db9e6901b")) + ui.Copy("flow_e8741f0d80") + Mathf.CeilToInt(Mathf.Max(0, watchSeconds - elapsed)) + ui.Copy("flow_a0f1490a20");
                ui.wristStatus.text = ui.Copy("flow_69063bd02f") + Mathf.CeilToInt(Mathf.Max(0, watchSeconds - elapsed)) + ui.Copy("flow_a0f1490a20");
                ui.SetProgress(elapsed / watchSeconds); yield return null;
            }
            rig.SetMovementLocked(false); BeginClues();
        }

        void BeginClues() {
            cluePage = 0; guessPage = 0;
            SetStep(PlaygroundStep.CollectClues); SpeakStep(3);
            ui.Show(ui.Copy("flow_327c8ebefd"), ui.Copy("flow_bdf41ee18e"), ui.Copy("flow_98ffc9fce7"));
            ui.SetPrimary(ui.Copy("flow_3e0e86c629"), OpenClueTool); ui.SetWristTools(true, false, OpenClueTool, null); ui.wristStatus.text = ui.Copy("flow_c9ec67c27f");
        }
        public void OpenClueTool() {
            if (CurrentStep != PlaygroundStep.CollectClues) return;
            RenderClueTool(); PlayInteraction(interactionVoiceovers?.clueToolOpened);
        }
        void RenderClueTool() {
            if (CurrentStep != PlaygroundStep.CollectClues) return;
            ui.Show(ui.Copy("flow_0b35eec322"), ui.Copy("flow_09ecbced61"), session.Data.clues.Count + ui.Copy("flow_854c6545ac"), true);
            bool[] selected = new bool[Clues.Length]; for (int i = 0; i < Clues.Length; i++) selected[i] = session.Data.clues.Contains(Clues[i]);
            DrawChoicePage(Clues, selected, cluePage, SelectClue);
            ui.footer.text = ui.Copy("flow_108d9ea56d") + (cluePage + 1) + ui.Copy("flow_607f2de6ce") + ChoicePageCount(Clues.Length) + ui.Copy("flow_d9da7e5bd7");
            ui.SetSecondary(cluePage == 0 ? ui.Copy("flow_d36a6f5bb3") : ui.Copy("flow_88bdafb8b0"), NextCluePage);
            ui.SetPrimary(ui.Copy("flow_7300f7a436"), CompleteClues, session.Data.clues.Count >= 3, session.Data.clues.Count >= 3);
            ui.SetWristTools(true, false, OpenClueTool, null); ui.wristStatus.text = ui.Copy("flow_dc42fd7a4f") + session.Data.clues.Count + ui.Copy("flow_3dc1a09625");
        }
        public void SelectClue(int index) {
            if (CurrentStep != PlaygroundStep.CollectClues || index < 0 || index >= Clues.Length) return;
            string clue = Clues[index]; bool removing = session.Data.clues.Contains(clue);
            if (removing) session.Data.clues.Remove(clue); else session.Data.clues.Add(clue);
            session.Record("CollectClues", removing ? "ClueRemoved" : "ClueSelected", clue); RenderClueTool(); PlayInteraction(removing ? interactionVoiceovers?.clueRemoved : interactionVoiceovers?.clueSelected);
            if (!removing) session.Record("CollectClues", "ClueEvidence", clue + " / " + (ClueObserved[index] ? "Observed in this scenario" : "Not observed in this scenario"));
        }
        int ChoicePageCount(int count) { return Mathf.CeilToInt((float)count / Mathf.Max(1, ui.choices.Length)); }
        void DrawChoicePage(string[] cards, bool[] selected, int page, Action<int> onSelect) {
            int start = page * ui.choices.Length;
            int count = Mathf.Min(ui.choices.Length, cards.Length - start);
            var labels = new string[count]; var marked = new bool[count];
            Array.Copy(cards, start, labels, 0, count); Array.Copy(selected, start, marked, 0, count);
            ui.DrawChoices(labels, marked, localIndex => onSelect(start + localIndex));
        }
        public void NextCluePage() {
            if (CurrentStep != PlaygroundStep.CollectClues) return;
            cluePage = (cluePage + 1) % ChoicePageCount(Clues.Length);
            session.Record("CollectClues", "CardPage", (cluePage + 1).ToString()); RenderClueTool(); PlayInteraction(interactionVoiceovers?.cluePageChanged);
        }
        public void NextGuessPage() {
            if (CurrentStep != PlaygroundStep.NoteGuesses && !(CurrentStep == PlaygroundStep.TestGuesses && reviewingGuesses)) return;
            guessPage = (guessPage + 1) % ChoicePageCount(GuessCards.Length);
            session.Record(CurrentStep.ToString(), "CardPage", (guessPage + 1).ToString()); RenderGuessTool(); PlayInteraction(interactionVoiceovers?.guessPageChanged);
        }
        public void CompleteClues() {
            if (CurrentStep != PlaygroundStep.CollectClues || session.Data.clues.Count < 3) { session.Record(CurrentStep.ToString(), "AdvanceBlocked", "At least three unique clues required"); return; }
            BeginGuesses();
        }
        void BeginGuesses() {
            SetStep(PlaygroundStep.NoteGuesses); SpeakStep(4); pendingGuess = -1;
            ui.Show(ui.Copy("flow_38e8f250a9"), ui.Copy("flow_6525d37bb2"), ui.Copy("flow_bbe21527ee"));
            ui.SetPrimary(ui.Copy("flow_8ddc58bc4e"), OpenGuessTool); ui.SetWristTools(false, true, null, OpenGuessTool); ui.wristStatus.text = ui.Copy("flow_c8330392e8");
        }
        public void OpenGuessTool() {
            if (CurrentStep != PlaygroundStep.NoteGuesses && !(CurrentStep == PlaygroundStep.TestGuesses && reviewingGuesses)) return;
            RenderGuessTool(); PlayInteraction(interactionVoiceovers?.guessToolOpened);
        }
        void RenderGuessTool() {
            if (CurrentStep != PlaygroundStep.NoteGuesses && !(CurrentStep == PlaygroundStep.TestGuesses && reviewingGuesses)) return;
            pendingGuess = -1;
            ui.Show(reviewingGuesses ? ui.Copy("flow_258f62f5d8") : ui.Copy("flow_b905aea83e"), reviewingGuesses ? ui.Copy("flow_4cc641b191") : ui.Copy("flow_7660bd5ad4"), ui.Copy("flow_2bf54b6d4f") + session.Data.guesses.Count, true);
            bool[] selected = new bool[GuessCards.Length]; for (int i = 0; i < GuessCards.Length; i++) selected[i] = FindGuess(GuessCards[i]) != null;
            DrawChoicePage(GuessCards, selected, guessPage, SelectGuess);
            ui.footer.text = ui.Copy("flow_108d9ea56d") + (guessPage + 1) + ui.Copy("flow_607f2de6ce") + ChoicePageCount(GuessCards.Length) + "  |  " + session.Data.guesses.Count + ui.Copy("flow_5b4d15ab4d");
            ui.SetSecondary(guessPage == 0 ? ui.Copy("flow_97ac18780d") : ui.Copy("flow_6265dba28d"), NextGuessPage);
            ui.SetPrimary(reviewingGuesses ? ui.Copy("flow_da2611211c") : ui.Copy("flow_bde171e0c3"), reviewingGuesses ? (UnityAction)CompleteGuessReview : CompleteGuesses, session.Data.guesses.Count >= 1);
            ui.SetWristTools(false, true, null, OpenGuessTool);
            ui.wristStatus.text = reviewingGuesses ? ui.Copy("flow_2389f2838e") : ui.Copy("flow_2bf54b6d4f") + session.Data.guesses.Count;
        }
        public void SelectGuess(int index) {
            if ((CurrentStep != PlaygroundStep.NoteGuesses && !reviewingGuesses) || index < 0 || index >= GuessCards.Length) return;
            guessPage = index / ui.choices.Length;
            pendingGuess = index; var existing = FindGuess(GuessCards[index]);
            ui.Show(ui.Copy("flow_e6e08e3c25"), GuessCards[index], ui.Copy("flow_3596a62021"), true);
            bool[] selected = { existing != null && existing.confidence == 0, existing != null && existing.confidence == 1, existing != null && existing.confidence == 2 };
            PlayInteraction(interactionVoiceovers?.guessSelected); ui.DrawChoices(Confidence, selected, SetGuessConfidence); ui.SetSecondary(ui.Copy("flow_9f5bd4b5f1"), OpenGuessTool);
        }
        public void SetGuessConfidence(int confidence) {
            if (pendingGuess < 0 || confidence < 0 || confidence > 2) return;
            string card = GuessCards[pendingGuess]; var guess = FindGuess(card);
            if (guess == null) { guess = new PlaygroundGuess { idea = card }; session.Data.guesses.Add(guess); }
            guess.confidence = confidence; session.Record(CurrentStep.ToString(), "GuessConfidence", card + " / " + Confidence[confidence]);
            session.Record(CurrentStep.ToString(), "GuessEvidence", card + " / " + GuessEvidence[pendingGuess]); RenderGuessTool(); PlayInteraction(interactionVoiceovers?.confidenceSaved);
        }
        PlaygroundGuess FindGuess(string idea) { return session.Data.guesses.Find(g => g.idea == idea); }
        string GuessSummary() { if (session.Data.guesses.Count == 0) return ui.noGuessesSelectedText; var b = new StringBuilder(); int count = Mathf.Min(3, session.Data.guesses.Count); for (int i = 0; i < count; i++) { var g = session.Data.guesses[i]; b.AppendLine(g.idea + " - " + g.ConfidenceLabel); } if (session.Data.guesses.Count > count) b.AppendLine("+ " + (session.Data.guesses.Count - count) + ui.Copy("flow_e0e9e74383")); return b.ToString().Trim(); }
        string ClueSummary() { if (session.Data.clues.Count == 0) return ui.noCluesSelectedText; int count = Mathf.Min(3, session.Data.clues.Count); var shown = session.Data.clues.GetRange(0, count); return string.Join(", ", shown) + (session.Data.clues.Count > count ? " (+ " + (session.Data.clues.Count - count) + ui.Copy("flow_e846f804c3") : ""); }
        public void CompleteGuesses() {
            if (CurrentStep != PlaygroundStep.NoteGuesses || session.Data.guesses.Count < 1 || pendingGuess >= 0) return;
            BeginResponseStep();
        }
        void BeginResponseStep() {
            SetStep(PlaygroundStep.TestGuesses); responseReady = false; responding = false; reviewingGuesses = false; SetStandAreaVisible(approachCircle, true); SpeakStep(5);
            ui.Show(ui.Copy("flow_a30abe1450"), ui.Copy("flow_b1b3445216"), ui.Copy("flow_196b483580"));
            ui.wristStatus.text = ui.Copy("flow_a30abe1450");
        }

        void ShowResponses() {
            ui.Show(ui.Copy("flow_d78401ce12"), ui.Copy("flow_2c0c8d4e26"), ui.Copy("flow_6b29078d58"), true);
            PlayInteraction(interactionVoiceovers?.responseChoicesShown); ui.DrawChoices(ResponseCards, null, i => ChooseResponse((PlaygroundResponse)i)); ui.wristStatus.text = ui.Copy("flow_aaedda1222");
        }
        public void ChooseResponse(PlaygroundResponse response) {
            if (CurrentStep != PlaygroundStep.TestGuesses || !responseReady || responding || reviewingGuesses) return;
            responding = true; responseReady = false;
            session.Data.firstResponse = ResponseCards[(int)response]; session.Data.firstResponseSeconds = Mathf.Max(0, Time.time - firstPopAt);
            session.Record("TestGuesses", "ResponseSelected", session.Data.firstResponse);
            if (response == PlaygroundResponse.EarDefenders) child.OfferDefenders();
            if (response == PlaygroundResponse.MoveAway) child.MoveToQuietSpot(quietPoint);
            routine = StartCoroutine(ObserveResponse(response));
        }
        IEnumerator ObserveResponse(PlaygroundResponse response) {
            ui.Show(ui.Copy("flow_e8837a189e"), ui.Copy("flow_168e66b480") + session.Data.firstResponse + ui.Copy("flow_61a4cac9b5"), ui.Copy("flow_d828559e10"));
            rig.SetMovementLocked(false); if (interactionVoiceovers) interactionVoiceovers.PlayResponseCardSequence(response);
            float start = Time.time; bool midpoint = false; bool positive = (int)response <= 2;
            while (Time.time - start < responseSeconds) {
                float elapsed = Time.time - start;
                if (!midpoint && elapsed >= ResponseMidpointTime) {
                    midpoint = true; PlayInteraction(interactionVoiceovers?.responseMidpoint); child.SetState(positive ? PlaygroundChildState.Calming : response == PlaygroundResponse.LoudVoiceOrSuddenTouch ? PlaygroundChildState.Escalated : PlaygroundChildState.Distressed);
                }
                ui.SetProgress(elapsed / responseSeconds); ui.status.text = ui.Copy("flow_8dfaeb8653") + Mathf.CeilToInt(Mathf.Max(0, responseSeconds - elapsed)) + ui.Copy("flow_a0f1490a20");
                ui.wristStatus.text = ui.Copy("flow_abff0e4557") + Mathf.CeilToInt(Mathf.Max(0, responseSeconds - elapsed)) + ui.Copy("flow_a0f1490a20"); yield return null;
            }
            child.SetState(positive ? PlaygroundChildState.Calm : response == PlaygroundResponse.LoudVoiceOrSuddenTouch ? PlaygroundChildState.Escalated : PlaygroundChildState.Distressed);
            session.Data.firstOutcome = positive ? ui.Copy("flow_c4e5b6ff05") : response == PlaygroundResponse.WaitAndWatch ? ui.Copy("flow_4e39d6d292") : ui.Copy("flow_71b2b30b68");
            session.Record("TestGuesses", "ResponseObserved", session.Data.firstOutcome); rig.SetMovementLocked(false);
            responding = false; reviewingGuesses = true; RenderGuessTool(); PlayInteraction(interactionVoiceovers?.guessReviewStarted);
        }
        public void CompleteGuessReview() {
            if (CurrentStep != PlaygroundStep.TestGuesses || !reviewingGuesses || pendingGuess >= 0) return;
            BeginChildPerspectiveStep();
        }
        void BeginChildPerspectiveStep() {
            reviewingGuesses = false; SetStep(PlaygroundStep.ChildPerspective); PlayInteraction(interactionVoiceovers?.childPerspectiveOffered);
            ui.Show(ui.Copy("flow_fe91f3b2bb"), ui.Copy("flow_8d54d516af"), ui.Copy("flow_6a7ca301eb"));
            ui.SetPrimary(ui.Copy("flow_fe91f3b2bb"), StartChildPerspective); ui.wristStatus.text = ui.Copy("flow_9157bbced8");
        }
        public void StartChildPerspective() {
            if (CurrentStep != PlaygroundStep.ChildPerspective || rig.InChildView) return;
            childViewPoint.position = child.transform.position;
            Vector3 direction = partyFocus.position - childViewPoint.position; direction.y = 0; childViewPoint.rotation = Quaternion.LookRotation(direction);
            if (perspectiveBodies) perspectiveBodies.EnterChildView(rig);
            rig.EnterChildView(childViewPoint, 1.25f);
            for (int i = 0; i < perspectiveProps.Length; i++) {
                var prop = perspectiveProps[i]; prop.localScale = propScales[i] * 1.75f;
                Vector3 closer = childViewPoint.position - prop.position; closer.y = 0; prop.position += closer.normalized * .7f;
                var anchor = prop.GetComponent<PlaygroundSurfaceAnchor>(); if (anchor) anchor.SnapToSupport();
            }
            partyMusic.volume = .32f; partyMusic.pitch = 1.12f; crowd.volume = .18f; ui.SetTint(true); SpeakStep(6);
            returnReady = false;
            ui.Show(ui.Copy("flow_c186ab9cbf"), ui.Copy("flow_0f2b97e17e"), ui.Copy("flow_7435b92a71"));
            ui.SetChildViewReturn(true, false, ReturnToMyView);
            routine = StartCoroutine(PerspectiveTimer()); session.Record("ChildPerspective", "ChildViewEntered", "Static view/tint; tracked headset remains active");
        }
        IEnumerator PerspectiveTimer() {
            float start = Time.time;
            while (Time.time - start < perspectiveSeconds) { float elapsed = Time.time - start; ui.SetProgress(elapsed / perspectiveSeconds); ui.status.text = ui.Copy("flow_9ab01a8f0f") + Mathf.CeilToInt(Mathf.Max(0, perspectiveSeconds - elapsed)) + ui.Copy("flow_0f7854229f"); ui.wristStatus.text = ui.Copy("flow_fb6e10faee") + Mathf.CeilToInt(Mathf.Max(0, perspectiveSeconds - elapsed)) + ui.Copy("flow_a0f1490a20"); yield return null; }
            returnReady = true; PlayInteraction(interactionVoiceovers?.returnToParentReady); ui.status.text = ui.Copy("flow_1ced32bd56"); ui.wristStatus.text = ui.Copy("flow_19afbe5880"); ui.SetPrimary(ui.Copy("flow_3c347c3e65"), ReturnToMyView); ui.SetChildViewReturn(true, true, ReturnToMyView);
        }
        public void ReturnToMyView() {
            if (CurrentStep != PlaygroundStep.ChildPerspective || !returnReady) return;
            ui.SetChildViewReturn(false, false, null);
            rig.ReturnToParentView(); if (perspectiveBodies) perspectiveBodies.RestoreParentView(); ui.SetTint(false); RestorePerspectiveProps(); partyMusic.pitch = 1; partyMusic.volume = .18f; crowd.volume = .09f;
            session.Record("ChildPerspective", "ParentViewRestored"); BeginOnlooker();
        }
        void RestorePerspectiveProps() { if (propPositions == null) return; for (int i = 0; i < perspectiveProps.Length; i++) { if (!perspectiveProps[i]) continue; perspectiveProps[i].localPosition = propPositions[i]; perspectiveProps[i].localScale = propScales[i]; perspectiveProps[i].localRotation = propRotations[i]; } }

        void BeginOnlooker() {
            if (thoughtViewedOnce) { BeginReflection(); return; }
            SetStep(PlaygroundStep.OnlookerThought); thoughtOpen = false; SpeakStep(7);
            ui.Show(ui.Copy("flow_0028ce8de4"), ui.Copy("flow_af7049bec8"), ui.Copy("flow_40dbc88ab2"));
            ui.wristStatus.text = ui.Copy("flow_f2ce014375"); CreateThoughtGaze();
        }
        void CreateThoughtGaze() {
            if (thoughtViewedOnce) return;
            int token = ++gazeToken;
            PlayStartInteraction(interactionVoiceovers?.thoughtGazeStarted);
            FactoryManager.CreateGaze(gazeThought, () => { if (token != gazeToken || CurrentStep != PlaygroundStep.OnlookerThought || thoughtOpen) return; OpenThoughtBubble(); });
        }
        public void OpenThoughtBubble() {
            if (CurrentStep != PlaygroundStep.OnlookerThought || thoughtOpen || thoughtViewedOnce) return;
            if (interactionVoiceovers) interactionVoiceovers.CancelWaitingStart(); thoughtOpen = true; thoughtViewedOnce = true;
            ui.wristStatus.text = ui.Copy("flow_b328ed4417");
            ui.ShowThought(ui.Copy("flow_1996f4d889"), CloseThoughtBubble);
            session.Record("OnlookerThought", "ThoughtBubbleOpened"); PlayInteraction(interactionVoiceovers?.thoughtBubbleOpened);
        }
        public void CloseThoughtBubble() {
            if (CurrentStep != PlaygroundStep.OnlookerThought || !thoughtOpen) return;
            thoughtOpen = false; ui.thoughtCanvas.gameObject.SetActive(false);
            gazeToken++; ClearGazeChildren(gazeThought);
            if (interactionVoiceovers) interactionVoiceovers.CancelPending();
            if (voice) voice.Stop();
            session.Record("OnlookerThought", "ThoughtBubbleClosed", "Viewed once; reflection questions shown immediately");
            BeginReflection(true);
        }

        void InitializeAnswerRecording() {
            if (!answerRecorder) answerRecorder = GetComponent<PlaygroundAnswerRecorder>();
            if (!answerRecorder) answerRecorder = gameObject.AddComponent<PlaygroundAnswerRecorder>();
            answerRecorder.ClipSaved -= OnAnswerClipSaved; answerRecorder.StateChanged -= RefreshAnswerRecordingUI;
            answerRecorder.ClipSaved += OnAnswerClipSaved; answerRecorder.StateChanged += RefreshAnswerRecordingUI;
        }
        void BeginReflection(bool playThoughtClosedVoiceover = false) {
            SetStep(PlaygroundStep.Reflect); reflectionIndex = 0; reflectionFeedback = false; ShowReflectionQuestion(true, playThoughtClosedVoiceover);
        }
        void ShowReflectionQuestion(bool playIntroduction = false, bool playThoughtClosedVoiceover = false) {
            questionNarrating = true;
            answerRecorder.ClearStatus();
            ui.ShowRecordingQuestion(ReflectionQuestions[reflectionIndex], reflectionIndex, ToggleAnswerRecording, NextReflectionQuestion, questionNarrating);
            lastAnswerUiSecond = -1; RefreshAnswerRecordingUI();
            session.Record("Reflect", "QuestionShown", ReflectionQuestions[reflectionIndex]);
            if (questionVoiceRoutine != null) StopCoroutine(questionVoiceRoutine);
            questionVoiceRoutine = StartCoroutine(NarrateReflectionQuestion(playIntroduction, playThoughtClosedVoiceover));
        }
        IEnumerator NarrateReflectionQuestion(bool introduction, bool playThoughtClosedVoiceover = false) {
            if (interactionVoiceovers) {
                if (playThoughtClosedVoiceover) yield return interactionVoiceovers.PlayAndWait(interactionVoiceovers.thoughtBubbleClosed);
                if (introduction) yield return interactionVoiceovers.PlayAndWait(interactionVoiceovers.reflectionIntroduction);
                yield return interactionVoiceovers.PlayAndWait(interactionVoiceovers.GetQuestionCue(reflectionIndex + 1));
            }
            questionNarrating = false; questionVoiceRoutine = null; RefreshAnswerRecordingUI();
        }
        void RefreshAnswerRecordingUI() {
            if (!ui || CurrentStep != PlaygroundStep.Reflect || reflectionFeedback || reflectionIndex > 2 || !answerRecorder) return;
            int count = answerRecorder.SavedCount(reflectionIndex + 1);
            ui.SetAnswerPrerequisites(questionNarrating, answerSavedByUser[reflectionIndex]);
            ui.UpdateAnswerRecordingState(answerRecorder.IsRecording, answerRecorder.IsSaving, count, answerRecorder.RecordedSeconds, answerRecorder.LastError);
            ui.wristStatus.text = ui.Copy("flow_4027cd489f") + (reflectionIndex + 1) + " / 3  /  " + (answerRecorder.IsRecording ? ui.Copy("flow_b299b63d6b") : count + ui.Copy("flow_2f4c2ff0b7"));
            if (questionNarrating) {
                ui.recordingPlaceholder.interactable = false; ui.skipQuestion.interactable = false;
                ui.status.text = ui.Copy("flow_f0a78f739a");
                ui.wristStatus.text = ui.Copy("flow_c383d7bbec") + (reflectionIndex + 1) + " / 3";
            }
        }
        public void ToggleAnswerRecording() {
            if (CurrentStep != PlaygroundStep.Reflect || reflectionFeedback || reflectionIndex > 2 || questionNarrating || !answerRecorder || answerRecorder.IsSaving) return;
            if (answerRecorder.IsRecording) { answerRecorder.StopAndSave(); return; }
            try {
                if (!recording || !FolderManager.IsValidId(recording.LockedParticipantId)) throw new InvalidOperationException("Confirm your participant ID and start the session before recording an answer.");
                string folder = string.IsNullOrEmpty(recording.SessionOutputFolder) ? recording.ResolveOutputFolder() : recording.SessionOutputFolder;
                if (answerRecorder.BeginAnswer(reflectionIndex + 1, folder, GetReflectionQuestionVoiceover(reflectionIndex + 1))) {
                    session.Record("Reflect", "AnswerRecordingStarted", "Q" + (reflectionIndex + 1) + "; next clip=" + (answerRecorder.SavedCount(reflectionIndex + 1) + 1));
                }
                RefreshAnswerRecordingUI();
            } catch (Exception ex) { ui.status.text = ex.Message; session.Record("Reflect", "AnswerRecordingError", ex.Message); }
        }
        void OnAnswerClipSaved(PlaygroundAnswerClip clip) {
            if (!session || session.Data == null) return;
            if (clip.question >= 1 && clip.question <= answerSavedByUser.Length && clip.stopReason != null && clip.stopReason.StartsWith("User stopped recording", StringComparison.Ordinal)) answerSavedByUser[clip.question - 1] = true;
            session.Data.reflectionClips.Add(clip);
            var names = new List<string>(); foreach (var item in session.Data.reflectionClips) if (item.question == clip.question) names.Add(item.fileName);
            session.Data.reflectionAnswers[clip.question - 1] = string.Join("\n", names);
            PlayInteraction(interactionVoiceovers?.answerSaved); session.Record("Reflect", "AnswerClipSaved", "Q" + clip.question + "; clip=" + clip.clip + "; seconds=" + clip.seconds.ToString("F2") + "; " + clip.path);
            RefreshAnswerRecordingUI();
        }
        string ReflectionRecordingSummary() {
            return ui.Copy("flow_8d3a6f2075") + answerRecorder.SavedCount(1) + ui.Copy("flow_08f13c76e8") + answerRecorder.SavedCount(2) + ui.Copy("flow_61de1bb8f7") + answerRecorder.SavedCount(3) + ui.Copy("flow_753cfa97f5");
        }
        public void NextReflectionQuestion() {
            if (CurrentStep != PlaygroundStep.Reflect || reflectionFeedback || reflectionIndex > 2 || questionNarrating || !answerRecorder || answerRecorder.IsRecording || answerRecorder.IsSaving) return;
            if (!answerSavedByUser[reflectionIndex] || answerRecorder.SavedCount(reflectionIndex + 1) < 1) { RefreshAnswerRecordingUI(); return; }
            session.Record("Reflect", "QuestionAnswered", ReflectionQuestions[reflectionIndex] + "; clips=" + answerRecorder.SavedCount(reflectionIndex + 1)); reflectionIndex++;
            if (reflectionIndex < 3) { ShowReflectionQuestion(); return; }
            reflectionFeedback = true; PlayInteraction(interactionVoiceovers?.reflectionFeedbackShown);
            string feedback = ui.Copy("flow_88b1db6b71") + ClueSummary() + ui.Copy("flow_493975531f") + GuessSummary() + ui.Copy("flow_c625360168") + session.Data.firstResponse + ".\n\n" + ReflectionRecordingSummary();
            ui.Show(ui.Copy("flow_39fddabbbc"), feedback, ui.Copy("flow_c2e04525b9"));
            ui.SetPrimary(ui.Copy("flow_3e57f1badc"), PrepareReplay); ui.wristStatus.text = ui.Copy("flow_dcf585e53f");
        }

        public void PrepareReplay() {
            if (CurrentStep != PlaygroundStep.Reflect || !reflectionFeedback || session.Data.replayCount > 0) return;
            SetStep(PlaygroundStep.ReplayAndPrevent); SpeakStep(9);
            ui.Show(ui.Copy("flow_3e57f1badc"), ui.Copy("flow_fc1b7fbb14"), ui.Copy("flow_1ad766bac6"));
            ui.SetPrimary(ui.Copy("flow_64cf2b2d79"), StartReplay); ui.wristStatus.text = ui.Copy("flow_bbf0841817");
        }
        public void StartReplay() {
            if (CurrentStep != PlaygroundStep.ReplayAndPrevent || replayStarted || session.Data.replayCount != 0) return;
            replayStarted = true; session.Data.replayCount = 1; replayAreaReached = false; replayPop = false; replayChoiceMade = false; replaySupportChosen = false;
            ui.ShowReplayAttention(); PlayInteraction(interactionVoiceovers?.replayStarted);
            child.ResetChild(); SetBalloonVisible(true); partyMusic.volume = .1f; crowd.volume = .04f;
            if (balloonPopVfx) balloonPopVfx.Stop();
            rig.SetMovementLocked(false); SetStandAreaVisible(observationCircle, true); ui.SetPrimary(ui.Copy("flow_b47ca6bd00"), null, false);
            ui.status.text = ui.Copy("flow_c371a55aa5"); ui.wristStatus.text = ui.Copy("flow_980dac031b");
            session.Record("ReplayAndPrevent", "ReplayStarted", "No introductory gazes; movement remains enabled");
        }
        IEnumerator RunReplay() {
            replayStartedAt = Time.time;
            ui.Show(ui.Copy("flow_7d62fe823e"), ui.Copy("flow_c910a2a15a"), ui.Copy("flow_940f79e6b6"), true);
            ui.DrawChoices(ReplayCards, null, ChooseReplayPrevention); PlayInteraction(interactionVoiceovers?.replayObservationStarted);
            float total = replayWarningSeconds + replayAfterPopSeconds; bool warned = false;
            while (Time.time - replayStartedAt < total) {
                float elapsed = Time.time - replayStartedAt;
                if (!warned && elapsed >= ReplayEarlyWarningTime) { warned = true; PlayInteraction(interactionVoiceovers?.replayEarlyWarningShown); crowd.volume = .1f; if (!replaySupportChosen) child.SetState(PlaygroundChildState.EarlyWarning); session.Record("ReplayAndPrevent", "EarlyCueShown"); }
                if (!replayPop && elapsed >= replayWarningSeconds) {
                    replayPop = true; PopBalloon(); PlayInteraction(interactionVoiceovers?.replayBalloonPopped); child.SetState(replaySupportChosen ? PlaygroundChildState.Calm : PlaygroundChildState.Distressed);
                    session.Record("ReplayAndPrevent", "PopOutcome", replaySupportChosen ? "Sound-reducing support in place" : "Sound exposure unchanged");
                }
                ui.SetProgress(elapsed / total);
                ui.status.text = !replayPop ? ui.Copy("flow_28ffbf4f0f") + Mathf.CeilToInt(Mathf.Max(0, replayWarningSeconds - elapsed)) + ui.Copy("flow_1225b060ae") : ui.Copy("flow_3a4f86cb07") + Mathf.CeilToInt(Mathf.Max(0, total - elapsed)) + ui.Copy("flow_55d7456070");
                ui.wristStatus.text = !replayPop ? ui.Copy("flow_3f79c2fdcd") + Mathf.CeilToInt(Mathf.Max(0, replayWarningSeconds - elapsed)) + ui.Copy("flow_a0f1490a20") : ui.Copy("flow_cabe34992a");
                yield return null;
            }
            session.Data.replayOutcome = replaySupportChosen ? (session.Data.replayIntervenedEarly ? ui.Copy("flow_a6ed360fae") : ui.Copy("flow_07eae3df24")) : replayChoiceMade ? ui.Copy("flow_84a3fc23b1") : ui.Copy("flow_adfa76a63b");
            ShowSummary();
        }
        public void ChooseReplayPrevention(int index) {
            if (CurrentStep != PlaygroundStep.ReplayAndPrevent || !replayAreaReached || replayChoiceMade || index < 0 || index >= ReplayCards.Length) return;
            PlayInteraction(interactionVoiceovers?.replayPreventionChosen); replayChoiceMade = true; replaySupportChosen = ReplayChoiceReducesSound(index);
            session.Data.replayIntervenedEarly = replaySupportChosen && !replayPop;
            session.Data.replaySecondsBeforePop = Mathf.Max(0, replayWarningSeconds - (Time.time - replayStartedAt));
            session.Data.replayResponse = ReplayCards[index];
            if (index == 0) child.OfferDefenders();
            else if (index == 2) child.MoveToQuietSpot(quietPoint);
            child.SetState(replaySupportChosen ? PlaygroundChildState.Calm : replayPop ? PlaygroundChildState.Distressed : PlaygroundChildState.EarlyWarning);
            bool[] selected = new bool[ReplayCards.Length]; selected[index] = true;
            ui.DrawChoices(ReplayCards, selected, null, false);
            ui.footer.text = ui.Copy("flow_15b56517e9");
            session.Record("ReplayAndPrevent", "PreventionSelected", session.Data.replayResponse + "; before pop=" + !replayPop + "; reduces sound exposure=" + replaySupportChosen);
        }
        void ShowSummary() {
            SetStep(PlaygroundStep.Summary); rig.SetMovementLocked(false); ui.SetTint(false); SetStandAreaVisible(observationCircle, false); SetStandAreaVisible(approachCircle, false);
            var d = session.Data;
            ui.ShowAttemptComparison(d, ui.Copy("flow_562273b653") + answerRecorder.SavedCount(1) + ui.Copy("flow_a097e25b64") + answerRecorder.SavedCount(2) + ui.Copy("flow_1c6d56015d") + answerRecorder.SavedCount(3) + ").");
            ui.SetPrimary(ui.Copy("flow_b222a722b9"), FinishModule); ui.wristStatus.text = ui.Copy("flow_407a3e5927"); ui.SetProgress(1);
            session.Finish(); PlayInteraction(interactionVoiceovers?.summaryShown);
            if (!completedEventSent) { completedEventSent = true; onModuleCompleted.Invoke(); }
        }
        public async void FinishModule() {
            if (CurrentStep != PlaygroundStep.Summary || finishingModule) return;
            finishingModule = true;
            if (recording && recording.recordModule) {
                ui.SetPrimary(ui.Copy("flow_8b341b28ba"), null, false);
                ui.status.text = ui.Copy("flow_4920fb5c54");
                await recording.StopAndSaveAsync("Finish module clicked");
                if (!this || !isActiveAndEnabled) return;
                if (recording.IsSaving) { finishingModule = false; return; }
            }
            CurrentStep = PlaygroundStep.Complete; ui.SetStep(11, "Complete"); ui.SetPrimary(ui.Copy("flow_e70e42b449"), null, false);
            ui.status.text = ui.Copy("flow_aa9a63e4e3") + (recording && !string.IsNullOrEmpty(recording.LastSavedPath) && (!recording.saveToQuest || !string.IsNullOrEmpty(recording.LastQuestSavedPath)) ? ui.Copy("flow_7fe14d460b") + recording.LockedParticipantId + "." : "");
            if (recording && recording.recordModule && string.IsNullOrEmpty(recording.LastSavedPath)) ui.status.text += "\nNo session video was saved. See recording status and Console.";
            if (recording && recording.QuestTransferPending) ui.status.text += "\nQuest transfer is pending. The temporary recording is retained safely.";
            if (recording && recording.returnToEntryOnCompletion) {
                var manager = FolderManager.Instance ? FolderManager.Instance : recording.folderManager;
                if (manager) await manager.ReturnToEntryAsync(recording);
            }
        }

        void SetBalloonVisible(bool visible) { if (eventBalloon) eventBalloon.SetActive(visible); if (balloonString) balloonString.SetActive(visible); }
        void PopBalloon() {
            SetBalloonVisible(false); pop.Play(); partyMusic.volume = .24f; crowd.volume = .13f;
            if (balloonPopVfx) { balloonPopVfx.gameObject.SetActive(true); balloonPopVfx.transform.position = popOrigin.position; balloonPopVfx.Reinit(); balloonPopVfx.Play(); }
            if (balloonPopVfxPrefab) {
                var fx = Instantiate(balloonPopVfxPrefab, popOrigin.position, popOrigin.rotation); spawnedPopEffects.Add(fx);
                foreach (var graph in fx.GetComponentsInChildren<VisualEffect>()) graph.Play();
                foreach (var particles in fx.GetComponentsInChildren<ParticleSystem>()) particles.Play();
                Destroy(fx, 10f);
            }
            onBalloonPopped.Invoke(); session.Record(CurrentStep.ToString(), "BalloonPopped", balloonPopVfx || balloonPopVfxPrefab ? "Optional VFX played" : "Optional VFX field is empty; sound and story event played");
        }
        void ClearGazeChildren(Transform anchor) { foreach (Transform c in anchor) if (c.GetComponent<GazeButton>()) { c.gameObject.SetActive(false); Destroy(c.gameObject); } }
        void OnDisable() {
            if (!initialized) return;
            gazeToken++; StopAllCoroutines(); if (interactionVoiceovers) interactionVoiceovers.CancelPending(); if (voice) voice.Stop();
            if (answerRecorder) {
                answerRecorder.StopAndSave("Module left / scene disabled");
                answerRecorder.ClipSaved -= OnAnswerClipSaved; answerRecorder.StateChanged -= RefreshAnswerRecordingUI;
            }
            if (recording) recording.RequestStopAndSave("Module left before Finish");
            if (perspectiveBodies) perspectiveBodies.RestoreParentView();
            if (rig) { rig.ReturnToParentView(); rig.SetMovementLocked(false); }
            if (ui) { ui.SetTint(false); ui.HideReplayAttention(); }
            RestorePerspectiveProps();
            foreach (var anchor in new[] { gazeChild, gazeOnlooker, gazeParty, gazeThought }) if (anchor) ClearGazeChildren(anchor);
            foreach (var fx in spawnedPopEffects) if (fx) Destroy(fx);
        }
    }
}
