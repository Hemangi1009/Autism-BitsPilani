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
        [Header("Story locations")]
        public Transform gazeChild, gazeOnlooker, gazeParty, gazeThought;
        public Transform observationPoint, approachPoint, quietPoint, childViewPoint, partyFocus;
        public GameObject observationCircle, approachCircle;
        public float arrivalRadius = .8f;
        [Header("Audio and narration")]
        public AudioSource voice, partyMusic, crowd, pop;
        public AudioClip[] stepVoiceovers = new AudioClip[10];
        public AudioClip changeVoice, responseVoice, replayHintVoice, summaryVoice;
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
        public float watchSeconds = 25f, responseSeconds = 20f, perspectiveSeconds = 25f;
        public float thoughtToReflectionSeconds = 4f;
        public float replayWarningSeconds = 15f, replayAfterPopSeconds = 8f;

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

        static readonly string[] Clues = { "Hands covering his ears", "His body has become tense", "Looking toward the speaker", "Loud party speaker", "Balloons near the slide", "Nearby crowd and voices" };
        static readonly string[] GuessCards = { "He finds the sound too loud", "He is scared of the balloons", "He wants to leave this area" };
        static readonly string[] Confidence = { "Not sure", "Somewhat sure", "Very sure" };
        static readonly string[] ResponseCards = { "Speak softly and ask gently", "Offer ear defenders", "Move him away from the speaker", "Wait quietly and watch", "Use a loud voice or sudden touch" };
        static readonly string[] ReflectionQuestions = { "What did you first think was happening?", "What changed your mind?", "Is something wrong with him?" };
        static readonly string[] StepCaptions = { "Getting ready", "Explore", "Watch what changes", "Collect clues", "Note guesses", "Test guesses", "Child view", "Onlooker thought", "Reflect", "Replay and prevent", "Summary", "Complete" };
        readonly List<GameObject> spawnedPopEffects = new List<GameObject>();
        Vector3[] propPositions, propScales;
        Quaternion[] propRotations;
        bool responseReady, reviewingGuesses, returnReady, thoughtOpen, reflectionFeedback;
        bool replayStarted, replayAreaReached, replayPop, replayChoiceMade, firstPop;
        bool responding;
        int introCount, reflectionIndex, gazeToken;
        float firstPopAt, replayStartedAt, thoughtClosedAt = -1;
        int pendingGuess = -1;
        Coroutine routine;
        bool initialized;
        bool completedEventSent;

        void Start() {
            if (!ValidateReferences()) { enabled = false; return; }
            initialized = true; session.BeginSession(); ui.Initialize(); child.CaptureRestPose();
            propPositions = new Vector3[perspectiveProps.Length]; propScales = new Vector3[perspectiveProps.Length]; propRotations = new Quaternion[perspectiveProps.Length];
            for (int i = 0; i < perspectiveProps.Length; i++) { propPositions[i] = perspectiveProps[i].localPosition; propScales[i] = perspectiveProps[i].localScale; propRotations[i] = perspectiveProps[i].localRotation; }
            observationCircle.SetActive(false); approachCircle.SetActive(false);
            partyMusic.volume = .10f; crowd.volume = .04f;
            if (balloonPopVfx) balloonPopVfx.Stop();
            partyMusic.Play(); crowd.Play();
            ui.wristHelp.onClick.AddListener(() => { ui.mainCanvas.gameObject.SetActive(true); ui.Recenter(); });
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
            CurrentStep = next; ui.SetStep((int)next, StepCaptions[(int)next]); ui.SetProgress(0);
            ui.wristStatus.text = StepCaptions[(int)next];
            ui.SetWristTools(false, false, null, null);
            session.Record(next.ToString(), "StepEntered");
        }
        void Speak(AudioClip clip) { if (!clip) return; voice.Stop(); voice.clip = clip; voice.Play(); session.Record(CurrentStep.ToString(), "Voiceover", clip.name); }
        void SpeakStep(int number) { if (number >= 0 && number < stepVoiceovers.Length) Speak(stepVoiceovers[number]); }

        IEnumerator LoadThenEnter() {
            SetStep(PlaygroundStep.Loading); rig.SetMovementLocked(true);
            ui.Show("Welcome to the playground", "The scene is getting ready. Narration will begin after the loading pause.", "Please wait at least six seconds.");
            ui.wristStatus.text = "Scene loading...";
            float elapsed = 0; float wait = Mathf.Max(6f, initialLoadSeconds);
            while (elapsed < wait) { elapsed += Time.deltaTime; ui.SetProgress(elapsed / wait); yield return null; }
            rig.SetMovementLocked(false); BeginIntro();
        }

        void BeginIntro() {
            SetStep(PlaygroundStep.EnterPlayground); introCount = 0; IntroGazesComplete = false;
            child.ResetChild(); SetBalloonVisible(true);
            ui.Show("Enter the playground", "Move around and look at three places in order: your child, the aunty on the bench, and the small birthday party.\n\nLook at each blue gaze marker until it completes. Then go to the highlighted circle near the slide.", "Look at your child's gaze marker first.");
            ui.hint.text = "VR: use the right trigger for UI. Desktop preview: WASD, Q/E, hold right mouse to look.";
            ui.wristStatus.text = "Look at child  /  0 of 3"; SpeakStep(1);
            int token = ++gazeToken;
            // Same FactoryManager Action callback chain as MySample; the package removes completed gaze prefabs.
            FactoryManager.CreateGaze(gazeChild, () => {
                if (token != gazeToken || CurrentStep != PlaygroundStep.EnterPlayground || introCount != 0) return;
                introCount = 1; session.Record("EnterPlayground", "GazeCompleted", "Child"); ui.wristStatus.text = "Look at aunty  /  1 of 3"; ui.status.text = "Now look at the aunty's gaze marker.";
                FactoryManager.CreateGaze(gazeOnlooker, () => {
                    if (token != gazeToken || CurrentStep != PlaygroundStep.EnterPlayground || introCount != 1) return;
                    introCount = 2; session.Record("EnterPlayground", "GazeCompleted", "Onlooker"); ui.wristStatus.text = "Look at party  /  2 of 3"; ui.status.text = "Now look at the birthday party's gaze marker.";
                    FactoryManager.CreateGaze(gazeParty, () => {
                        if (token != gazeToken || CurrentStep != PlaygroundStep.EnterPlayground || introCount != 2) return;
                        introCount = 3; IntroGazesComplete = true; observationCircle.SetActive(true);
                        ui.status.text = "All three noticed. Go to the highlighted circle near the slide."; ui.wristStatus.text = "3 of 3  /  Go to the circle";
                        session.Record("EnterPlayground", "GazeCompleted", "Party; observation circle enabled");
                    });
                });
            });
        }

        void Update() {
            if (!initialized) return;
            if (CurrentStep == PlaygroundStep.EnterPlayground && IntroGazesComplete && !voice.isPlaying && rig.IsAt(observationPoint, arrivalRadius)) {
                session.Record("EnterPlayground", "ObservationCircleReached"); observationCircle.SetActive(false); routine = StartCoroutine(WatchChanges());
            }
            if (CurrentStep == PlaygroundStep.TestGuesses && !responseReady && !responding && !reviewingGuesses && rig.IsAt(approachPoint, arrivalRadius)) {
                responseReady = true; approachCircle.SetActive(false); session.Record("TestGuesses", "ChildApproached"); ShowResponses();
            }
            if (CurrentStep == PlaygroundStep.OnlookerThought && !thoughtOpen && thoughtClosedAt >= 0 && Time.time - thoughtClosedAt >= thoughtToReflectionSeconds) {
                gazeToken++; ClearGazeChildren(gazeThought); BeginReflection();
            }
            if (CurrentStep == PlaygroundStep.ReplayAndPrevent && replayStarted && !replayAreaReached && rig.IsAt(observationPoint, arrivalRadius)) {
                replayAreaReached = true; observationCircle.SetActive(false); session.Record("ReplayAndPrevent", "ReplayCircleReached"); routine = StartCoroutine(RunReplay());
            }
        }

        IEnumerator WatchChanges() {
            SetStep(PlaygroundStep.WatchChanges); rig.Face(child.transform); rig.SetMovementLocked(true);
            ui.Show("Watch what changes", "Watch your child's body and the sounds around him. Controller movement and interaction are paused during this short scene. You can still look around.", "The crowd gets louder first. Watch for the early signs.");
            ui.wristStatus.text = "Watch your child"; SpeakStep(2);
            float start = Time.time; bool warned = false; firstPop = false;
            while (Time.time - start < watchSeconds) {
                float elapsed = Time.time - start;
                if (!warned && elapsed >= 4f) { warned = true; crowd.volume = .12f; child.SetState(PlaygroundChildState.EarlyWarning); session.Record("WatchChanges", "EarlyCue", "Hand to ear; crowd louder; still pose"); }
                if (!firstPop && elapsed >= 10f) { firstPop = true; firstPopAt = Time.time; PopBalloon(); child.SetState(PlaygroundChildState.Distressed); Speak(changeVoice); }
                ui.status.text = (firstPop ? "Balloon popped. Hands cover ears; the child's body is tense." : "Notice the early changes before the pop.") + "\nWatch time remaining: " + Mathf.CeilToInt(Mathf.Max(0, watchSeconds - elapsed)) + "s";
                ui.wristStatus.text = "Observe: " + Mathf.CeilToInt(Mathf.Max(0, watchSeconds - elapsed)) + "s";
                ui.SetProgress(elapsed / watchSeconds); yield return null;
            }
            rig.SetMovementLocked(false); BeginClues();
        }

        void BeginClues() {
            SetStep(PlaygroundStep.CollectClues); SpeakStep(3);
            ui.Show("Collect the clues", "The scene is holding at the foot of the slide. Open the clue tool on your left hand, then choose at least three different clues from the cards.", "Three unique clues are required.");
            ui.SetPrimary("Open clue tool", OpenClueTool); ui.SetWristTools(true, false, OpenClueTool, null); ui.wristStatus.text = "Clues: 0 / 3 required";
        }
        public void OpenClueTool() {
            if (CurrentStep != PlaygroundStep.CollectClues) return;
            ui.Show("Clue tool", "Choose what you noticed in the child and in the playground. Select at least three different cards.", session.Data.clues.Count + " unique clues selected.", true);
            bool[] selected = new bool[Clues.Length]; for (int i = 0; i < Clues.Length; i++) selected[i] = session.Data.clues.Contains(Clues[i]);
            ui.DrawChoices(Clues, selected, SelectClue);
            ui.SetPrimary("Done - collect clues", CompleteClues, session.Data.clues.Count >= 3);
            ui.SetWristTools(true, false, OpenClueTool, null); ui.wristStatus.text = "Clues: " + session.Data.clues.Count + " / 3 required";
        }
        public void SelectClue(int index) {
            if (CurrentStep != PlaygroundStep.CollectClues || index < 0 || index >= Clues.Length) return;
            string clue = Clues[index]; bool removing = session.Data.clues.Contains(clue);
            if (removing) session.Data.clues.Remove(clue); else session.Data.clues.Add(clue);
            session.Record("CollectClues", removing ? "ClueRemoved" : "ClueSelected", clue); OpenClueTool();
        }
        public void CompleteClues() {
            if (CurrentStep != PlaygroundStep.CollectClues || session.Data.clues.Count < 3) { session.Record(CurrentStep.ToString(), "AdvanceBlocked", "At least three unique clues required"); return; }
            SetStep(PlaygroundStep.NoteGuesses); SpeakStep(4); pendingGuess = -1;
            ui.Show("Note your guesses", "Open the guess tool. Choose at least one possible reason, then choose a confidence card.\n\nYou can add more than one idea.", "At least one guess with a confidence choice is required.");
            ui.SetPrimary("Open guess tool", OpenGuessTool); ui.SetWristTools(false, true, null, OpenGuessTool); ui.wristStatus.text = "Choose a guess and confidence";
        }
        public void OpenGuessTool() {
            if (CurrentStep != PlaygroundStep.NoteGuesses && !(CurrentStep == PlaygroundStep.TestGuesses && reviewingGuesses)) return;
            pendingGuess = -1;
            ui.Show(reviewingGuesses ? "Review your guesses" : "Guess tool", reviewingGuesses ? "What changed after your response? Choose an idea to update its confidence, or add another idea." : "What might be going on? Choose a card, then choose how sure you are.", "Guesses recorded: " + session.Data.guesses.Count, true);
            bool[] selected = new bool[GuessCards.Length]; for (int i = 0; i < GuessCards.Length; i++) selected[i] = FindGuess(GuessCards[i]) != null;
            ui.DrawChoices(GuessCards, selected, SelectGuess);
            ui.footer.text = GuessSummary();
            ui.SetPrimary(reviewingGuesses ? "Continue to child view" : "Done - test guesses", reviewingGuesses ? (UnityAction)CompleteGuessReview : CompleteGuesses, session.Data.guesses.Count >= 1);
            ui.SetWristTools(false, true, null, OpenGuessTool);
            ui.wristStatus.text = reviewingGuesses ? "Review guess confidence" : "Guesses recorded: " + session.Data.guesses.Count;
        }
        public void SelectGuess(int index) {
            if ((CurrentStep != PlaygroundStep.NoteGuesses && !reviewingGuesses) || index < 0 || index >= GuessCards.Length) return;
            pendingGuess = index; var existing = FindGuess(GuessCards[index]);
            ui.Show("How sure are you?", GuessCards[index], "Choose a confidence level. You can revise it after observing your child.", true);
            bool[] selected = { existing != null && existing.confidence == 0, existing != null && existing.confidence == 1, existing != null && existing.confidence == 2 };
            ui.DrawChoices(Confidence, selected, SetGuessConfidence); ui.SetSecondary("Back to guesses", OpenGuessTool);
        }
        public void SetGuessConfidence(int confidence) {
            if (pendingGuess < 0 || confidence < 0 || confidence > 2) return;
            string card = GuessCards[pendingGuess]; var guess = FindGuess(card);
            if (guess == null) { guess = new PlaygroundGuess { idea = card }; session.Data.guesses.Add(guess); }
            guess.confidence = confidence; session.Record(CurrentStep.ToString(), "GuessConfidence", card + " / " + Confidence[confidence]); OpenGuessTool();
        }
        PlaygroundGuess FindGuess(string idea) { return session.Data.guesses.Find(g => g.idea == idea); }
        string GuessSummary() { var b = new StringBuilder(); foreach (var g in session.Data.guesses) b.AppendLine(g.idea + " - " + g.ConfidenceLabel); return b.ToString().Trim(); }
        public void CompleteGuesses() {
            if (CurrentStep != PlaygroundStep.NoteGuesses || session.Data.guesses.Count < 1 || pendingGuess >= 0) return;
            SetStep(PlaygroundStep.TestGuesses); responseReady = false; responding = false; reviewingGuesses = false; approachCircle.SetActive(true); SpeakStep(5);
            ui.Show("Go to your child", "Go to the highlighted circle beside your child, about one metre away. Then choose how to respond using the cards.\n\nThere is no speaking or recording input in this step.", "Move to the child's approach circle.");
            ui.wristStatus.text = "Go to your child";
        }

        void ShowResponses() {
            ui.Show("Choose how to respond", "Choose one response, then watch for twenty seconds. Look at the child's hands and posture.", "You are near your child. Choose one response card.", true);
            ui.DrawChoices(ResponseCards, null, i => ChooseResponse((PlaygroundResponse)i)); ui.wristStatus.text = "Choose a response card";
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
            ui.Show("Watch how your child reacts", "Response: " + session.Data.firstResponse + "\n\nWatch for a change in his hands and body.", "Observing the response...");
            rig.SetMovementLocked(false); Speak(responseVoice);
            float start = Time.time; bool midpoint = false; bool positive = (int)response <= 2;
            while (Time.time - start < responseSeconds) {
                float elapsed = Time.time - start;
                if (!midpoint && elapsed >= responseSeconds * .5f) {
                    midpoint = true; child.SetState(positive ? PlaygroundChildState.Calming : response == PlaygroundResponse.LoudVoiceOrSuddenTouch ? PlaygroundChildState.Escalated : PlaygroundChildState.Distressed);
                }
                ui.SetProgress(elapsed / responseSeconds); ui.status.text = "Watch time remaining: " + Mathf.CeilToInt(Mathf.Max(0, responseSeconds - elapsed)) + "s";
                ui.wristStatus.text = "Response observation: " + Mathf.CeilToInt(Mathf.Max(0, responseSeconds - elapsed)) + "s"; yield return null;
            }
            child.SetState(positive ? PlaygroundChildState.Calm : response == PlaygroundResponse.LoudVoiceOrSuddenTouch ? PlaygroundChildState.Escalated : PlaygroundChildState.Distressed);
            session.Data.firstOutcome = positive ? "Hands lowered; child appeared calmer" : response == PlaygroundResponse.WaitAndWatch ? "Distress remained" : "Distress increased";
            session.Record("TestGuesses", "ResponseObserved", session.Data.firstOutcome); rig.SetMovementLocked(false);
            responding = false; reviewingGuesses = true; OpenGuessTool();
        }
        public void CompleteGuessReview() {
            if (CurrentStep != PlaygroundStep.TestGuesses || !reviewingGuesses || pendingGuess >= 0) return;
            reviewingGuesses = false; SetStep(PlaygroundStep.ChildPerspective);
            ui.Show("Look beyond the scene", "See the playground from your child's position. The speaker and balloons will appear larger and closer, with louder, sharper sound.\n\nThis is an illustration of a possible experience. A soft amber tint marks the child's viewpoint.", "Select the button to enter the child-perspective view.");
            ui.SetPrimary("Look beyond the scene", StartChildPerspective); ui.wristStatus.text = "Child-perspective view ready";
        }
        public void StartChildPerspective() {
            if (CurrentStep != PlaygroundStep.ChildPerspective || rig.InChildView) return;
            childViewPoint.position = child.transform.position;
            Vector3 direction = partyFocus.position - childViewPoint.position; direction.y = 0; childViewPoint.rotation = Quaternion.LookRotation(direction);
            rig.EnterChildView(childViewPoint, 1.25f);
            for (int i = 0; i < perspectiveProps.Length; i++) {
                var prop = perspectiveProps[i]; prop.localScale = propScales[i] * 1.35f;
                Vector3 closer = childViewPoint.position - prop.position; closer.y = 0; prop.position += closer.normalized * .7f;
                var anchor = prop.GetComponent<PlaygroundSurfaceAnchor>(); if (anchor) anchor.SnapToSupport();
            }
            partyMusic.volume = .32f; partyMusic.pitch = 1.12f; crowd.volume = .18f; ui.SetTint(true); SpeakStep(6);
            returnReady = false;
            ui.Show("From the child's position", "Look toward the speaker and the balloons. Notice how their size and the sharper sound change the scene.\n\nYou can look around. Return to My View will appear after the observation time.", "Static amber tint active; no flashes.");
            routine = StartCoroutine(PerspectiveTimer()); session.Record("ChildPerspective", "ChildViewEntered", "Static view/tint; tracked headset remains active");
        }
        IEnumerator PerspectiveTimer() {
            float start = Time.time;
            while (Time.time - start < perspectiveSeconds) { float elapsed = Time.time - start; ui.SetProgress(elapsed / perspectiveSeconds); ui.status.text = "Listen and look: " + Mathf.CeilToInt(Mathf.Max(0, perspectiveSeconds - elapsed)) + "s remaining"; ui.wristStatus.text = "Child view: " + Mathf.CeilToInt(Mathf.Max(0, perspectiveSeconds - elapsed)) + "s"; yield return null; }
            returnReady = true; ui.status.text = "You can return to your own view now."; ui.wristStatus.text = "Return to my view is ready"; ui.SetPrimary("Return to my view", ReturnToMyView);
        }
        public void ReturnToMyView() {
            if (CurrentStep != PlaygroundStep.ChildPerspective || !returnReady) return;
            rig.ReturnToParentView(); ui.SetTint(false); RestorePerspectiveProps(); partyMusic.pitch = 1; partyMusic.volume = .18f; crowd.volume = .09f;
            session.Record("ChildPerspective", "ParentViewRestored"); BeginOnlooker();
        }
        void RestorePerspectiveProps() { if (propPositions == null) return; for (int i = 0; i < perspectiveProps.Length; i++) { perspectiveProps[i].localPosition = propPositions[i]; perspectiveProps[i].localScale = propScales[i]; perspectiveProps[i].localRotation = propRotations[i]; } }

        void BeginOnlooker() {
            SetStep(PlaygroundStep.OnlookerThought); thoughtClosedAt = -1; thoughtOpen = false; SpeakStep(7);
            ui.Show("Hear what the onlooker thinks", "Look at the aunty on the bench. Open the blue gaze marker over her thought bubble. Read her assumption, then close the bubble. You can reopen it before reflection starts.", "Look at the thought-bubble gaze marker.");
            ui.wristStatus.text = "Look at aunty's thought bubble"; CreateThoughtGaze();
        }
        void CreateThoughtGaze() {
            int token = ++gazeToken;
            FactoryManager.CreateGaze(gazeThought, () => { if (token != gazeToken || CurrentStep != PlaygroundStep.OnlookerThought || thoughtOpen) return; OpenThoughtBubble(); });
        }
        public void OpenThoughtBubble() {
            if (CurrentStep != PlaygroundStep.OnlookerThought || thoughtOpen) return;
            thoughtOpen = true; thoughtClosedAt = -1;
            ui.wristStatus.text = "Onlooker's thought is open";
            ui.ShowThought("An onlooker's assumption\n\n\"Why is he covering his ears? Children should enjoy parties. Is something wrong with him?\"", CloseThoughtBubble);
            session.Record("OnlookerThought", "ThoughtBubbleOpened");
        }
        public void CloseThoughtBubble() {
            if (CurrentStep != PlaygroundStep.OnlookerThought || !thoughtOpen) return;
            thoughtOpen = false; ui.thoughtCanvas.gameObject.SetActive(false); thoughtClosedAt = Time.time;
            session.Record("OnlookerThought", "ThoughtBubbleClosed", "Reflection begins after four seconds unless reopened");
            ui.status.text = "You can reopen the bubble. Reflection will begin shortly."; CreateThoughtGaze();
            ui.wristStatus.text = "Reflection starts shortly";
        }

        void BeginReflection() {
            SetStep(PlaygroundStep.Reflect); reflectionIndex = 0; reflectionFeedback = false; SpeakStep(8); ShowReflectionQuestion();
        }
        void ShowReflectionQuestion() {
            ui.ShowRecordingQuestion(ReflectionQuestions[reflectionIndex], reflectionIndex, SkipReflectionQuestion);
            ui.wristStatus.text = "Reflection " + (reflectionIndex + 1) + " / 3  -  Skip available";
            session.Record("Reflect", "QuestionShown", ReflectionQuestions[reflectionIndex]);
        }
        public void SkipReflectionQuestion() {
            if (CurrentStep != PlaygroundStep.Reflect || reflectionFeedback || reflectionIndex > 2) return;
            session.Data.reflectionAnswers[reflectionIndex] = "Skipped - recording not implemented";
            session.Record("Reflect", "QuestionSkipped", ReflectionQuestions[reflectionIndex]); reflectionIndex++;
            if (reflectionIndex < 3) { ShowReflectionQuestion(); return; }
            reflectionFeedback = true;
            string feedback = "You noticed: " + string.Join(", ", session.Data.clues) + ".\n\nYour guesses:\n" + GuessSummary() + "\n\nLoud, sudden sounds may feel uncomfortable or overwhelming. His hands and body give clues; a guess is something to check, not a fact. An onlooker's assumption does not tell us what he needs.\n\nYour response: " + session.Data.firstResponse + ".\n\nThe three spoken-answer questions were skipped in this prototype.";
            ui.Show("Reflection feedback", feedback, "Next, replay once and try to act before the balloon pops.");
            ui.body.rectTransform.sizeDelta = new Vector2(900, 480); ui.body.rectTransform.anchoredPosition = new Vector2(0, 100);
            ui.status.rectTransform.anchoredPosition = new Vector2(0, -200);
            ui.SetPrimary("Replay and prevent", PrepareReplay); ui.wristStatus.text = "Reflection finished  /  Replay next";
        }

        public void PrepareReplay() {
            if (CurrentStep != PlaygroundStep.Reflect || !reflectionFeedback || session.Data.replayCount > 0) return;
            SetStep(PlaygroundStep.ReplayAndPrevent); SpeakStep(9);
            ui.Show("Replay and prevent", "You will replay the scene once. The three introductory gazes will not repeat.\n\nGo to the observation circle, watch the early cues, and use ear defenders or move your child away before the pop. You can move throughout this attempt.", "One replay is available. After it, the module ends at the summary.");
            ui.SetPrimary("Replay once", StartReplay); ui.wristStatus.text = "Replay 0 / 1";
        }
        public void StartReplay() {
            if (CurrentStep != PlaygroundStep.ReplayAndPrevent || replayStarted || session.Data.replayCount != 0) return;
            replayStarted = true; session.Data.replayCount = 1; replayAreaReached = false; replayPop = false; replayChoiceMade = false;
            child.ResetChild(); SetBalloonVisible(true); partyMusic.volume = .1f; crowd.volume = .04f;
            if (balloonPopVfx) balloonPopVfx.Stop();
            rig.SetMovementLocked(false); observationCircle.SetActive(true); ui.SetPrimary("Replay used", null, false);
            ui.status.text = "Go to the highlighted observation circle. You remain free to move."; ui.wristStatus.text = "Replay 1 / 1  /  Go to circle";
            session.Record("ReplayAndPrevent", "ReplayStarted", "No introductory gazes; movement remains enabled");
        }
        IEnumerator RunReplay() {
            replayStartedAt = Time.time;
            ui.Show("Act on the early signs", "Look for an early hand-to-ear cue and a change in posture. You can move freely. Choose a preventive response before the pop.", "Replay warning period has begun.", true);
            ui.DrawChoices(new[] { "Offer ear defenders early", "Move to a quieter spot early" }, null, ChooseReplayPrevention); Speak(replayHintVoice);
            float total = replayWarningSeconds + replayAfterPopSeconds; bool warned = false;
            while (Time.time - replayStartedAt < total) {
                float elapsed = Time.time - replayStartedAt;
                if (!warned && elapsed >= 3f) { warned = true; crowd.volume = .1f; if (!replayChoiceMade) child.SetState(PlaygroundChildState.EarlyWarning); session.Record("ReplayAndPrevent", "EarlyCueShown"); }
                if (!replayPop && elapsed >= replayWarningSeconds) {
                    replayPop = true; PopBalloon(); child.SetState(replayChoiceMade ? PlaygroundChildState.Calm : PlaygroundChildState.Distressed);
                    session.Record("ReplayAndPrevent", "PopOutcome", replayChoiceMade ? "Early support in place" : "No early support");
                }
                ui.SetProgress(elapsed / total);
                ui.status.text = !replayPop ? "Time before the pop: " + Mathf.CeilToInt(Mathf.Max(0, replayWarningSeconds - elapsed)) + "s. Movement is enabled." : "Observe the result. Summary in " + Mathf.CeilToInt(Mathf.Max(0, total - elapsed)) + "s.";
                ui.wristStatus.text = !replayPop ? "Pop in " + Mathf.CeilToInt(Mathf.Max(0, replayWarningSeconds - elapsed)) + "s" : "Replay 1 / 1 - observe result";
                yield return null;
            }
            session.Data.replayOutcome = replayChoiceMade ? (session.Data.replayIntervenedEarly ? "Early support; child remained calmer" : "Support offered after the pop") : "No intervention; distress remained";
            ShowSummary();
        }
        public void ChooseReplayPrevention(int index) {
            if (CurrentStep != PlaygroundStep.ReplayAndPrevent || !replayAreaReached || replayChoiceMade || index < 0 || index > 1) return;
            replayChoiceMade = true; session.Data.replayIntervenedEarly = !replayPop;
            session.Data.replaySecondsBeforePop = Mathf.Max(0, replayWarningSeconds - (Time.time - replayStartedAt));
            session.Data.replayResponse = index == 0 ? "Ear defenders" : "Moved to quieter spot";
            if (index == 0) child.OfferDefenders(); else child.MoveToQuietSpot(quietPoint);
            child.SetState(PlaygroundChildState.Calm);
            ui.DrawChoices(new[] { "Ear defenders selected", "Quieter spot selected" }, new[] { index == 0, index == 1 }, null, false);
            ui.footer.text = session.Data.replayIntervenedEarly ? "Support was offered before the pop." : "Support was offered after the pop.";
            session.Record("ReplayAndPrevent", "PreventionSelected", session.Data.replayResponse + "; before pop=" + session.Data.replayIntervenedEarly);
        }
        void ShowSummary() {
            SetStep(PlaygroundStep.Summary); rig.SetMovementLocked(false); ui.SetTint(false); observationCircle.SetActive(false); approachCircle.SetActive(false);
            var d = session.Data;
            string comparison = "FIRST ATTEMPT\nClues selected: " + d.clues.Count + "   |   Guesses recorded: " + d.guesses.Count + "\nResponse: " + d.firstResponse + "\nActed " + d.firstResponseSeconds.ToString("F1") + " seconds after the pop.\nResult: " + d.firstOutcome + "\n\nONE REPLAY\nResponse: " + d.replayResponse + "\n" + (d.replayIntervenedEarly ? "Acted " + d.replaySecondsBeforePop.ToString("F1") + " seconds before the pop." : "No support was selected before the pop.") + "\nResult: " + d.replayOutcome + "\n\nReflection recording remains a UI placeholder. No audio answers were recorded.";
            ui.Show("Module 1 summary", comparison, "Replay used: 1 of 1. The module is finished; no second replay is available.");
            ui.body.rectTransform.sizeDelta = new Vector2(900, 480); ui.body.rectTransform.anchoredPosition = new Vector2(0, 100);
            ui.status.rectTransform.anchoredPosition = new Vector2(0, -200);
            ui.SetPrimary("Finish module", FinishModule); ui.wristStatus.text = "Module finished  /  Replay 1 of 1"; ui.SetProgress(1);
            session.Finish(); Speak(summaryVoice);
            if (!completedEventSent) { completedEventSent = true; onModuleCompleted.Invoke(); }
        }
        public void FinishModule() {
            if (CurrentStep != PlaygroundStep.Summary) return;
            CurrentStep = PlaygroundStep.Complete; ui.SetStep(11, "Complete"); ui.SetPrimary("Module complete", null, false);
            ui.status.text = "Thank you. Module 1 is complete.";
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
            gazeToken++; StopAllCoroutines();
            if (rig) { rig.ReturnToParentView(); rig.SetMovementLocked(false); }
            if (ui) ui.SetTint(false);
            RestorePerspectiveProps();
            foreach (var anchor in new[] { gazeChild, gazeOnlooker, gazeParty, gazeThought }) if (anchor) ClearGazeChildren(anchor);
            foreach (var fx in spawnedPopEffects) if (fx) Destroy(fx);
        }
    }
}
