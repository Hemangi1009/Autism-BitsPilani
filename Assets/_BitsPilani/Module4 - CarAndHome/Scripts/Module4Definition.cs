using System;
using UnityEngine;
using UnityEngine.Events;
namespace BitsPilani.Module4 {
    public enum Module4Stage { HomeIntro, WalkObserve, CollectClues, NoteGuesses, HomeFlashback, Respond, ReviewGuesses, Reflect, Feedback, ReplayHome, ReplayWalk, Summary, Complete }
    public enum Module4Event { moduleStarted, participantAccepted, moduleCompleted, sceneLeaving, sceneArrived, homeStarted, childGazeStarted, childGazeCompleted, homeObservationStarted, shoesPutOn, usualThingsPickedUp, parentSaysLetsGo, homeObservationFinished, goToWalkSelected, walkStarted, walkObservationStarted, childLooksAtPark, parkPassed, childUpset, walkObservationFinished, clueStepStarted, clueToolOpened, clueSelected, clueRemoved, cluePageChanged, cluesCompleted, guessStepStarted, guessToolOpened, guessSelected, confidenceSaved, guessRemoved, guessPageChanged, guessesCompleted, lookBeyondOffered, lookBeyondSelected, childViewEntered, flashbackObservationFinished, returnToWalkReady, returnToWalkSelected, responseStepStarted, responseChoicesShown, cardPictureBoard, cardNotToday, cardKeepWalking, cardRaiseVoice, responseChosen, responseMidpoint, responseObservationFinished, guessReviewStarted, guessReviewCompleted, reflectionStarted, reflectionQuestion1, reflectionQuestion2, reflectionQuestion3, answerRecordingStarted, answerSaved, reflectionCompleted, feedbackShown, replayOffered, replaySelected, replayHomeStarted, replayPictureBoard, replayNotToday, expectationsSet, replayWalkStarted, replayParkPassed, replayWalkFinished, summaryShown, walkApproachStarted, walkCheckpointReached, replayFamiliarRoutine, replayExplainIfAsked }
    [Serializable] public sealed class Module4VoiceCue {
        [Tooltip("Optional. Empty means no voiceover for this event. No generated audio is used.")] public AudioClip clip;
        [Min(0), Tooltip("Delay from this event to its optional VO.")] public float delaySeconds;
        public UnityEvent onStarted = new UnityEvent();
        public UnityEvent onCompleted = new UnityEvent();
    }
    [CreateAssetMenu(menuName="BitsPilani/Module 4/Training definition")]
    public sealed class Module4Definition : ScriptableObject {
        [Header("Scene sequence")]
        public string homeScene = "Module4_Home";
        public string walkScene = "Module4_Walk";
        [Header("Durations in seconds - static event stages")]
        [Min(0)] public float initialLoadSeconds = 6;
        [Min(1)] public float homeObservationSeconds = 20;
        [Min(1)] public float walkObservationSeconds = 20;
        [Min(1)] public float flashbackSeconds = 20;
        [Min(1)] public float responseObservationSeconds = 10;
        [Min(1)] public float replayObservationSeconds = 20;
        [Min(0)] public float responseDialoguePauseSeconds = 2.5f;
        [Min(.1f)] public float fadeSeconds = .2f;
        [Header("Practice")]
        public bool guidedPractice = true;
        public bool requireParticipantId = true;
        public bool requireSavedAnswer = true;
        public bool childEyeView = true;
        [Range(.7f,1.6f)] public float childEyeHeight = 1.05f;
        [Header("Clue cards - selectable observations, not scored")]
        [TextArea] public string[] clues = {
            "He stopped walking", "He is crying without using words", "He keeps looking towards the park",
            "His body looks tense", "The park entrance is beside the path", "The mall is in the other direction",
            "No destination was explained at home", "He got ready in his usual going-out routine",
            "His shoes seem uncomfortable", "He seems tired after a long walk", "He may have noticed something unfamiliar", "He is asking for a snack"
        };
        [Header("Guess cards - several interpretations are possible")]
        [TextArea] public string[] guesses = {
            "He expected to visit the park", "He is scared of something nearby", "He wants to stop walking",
            "He does not like this place", "He does not know where we are going", "He finds an unexpected change difficult",
            "He is uncomfortable or tired", "He needs help understanding today's plan"
        };
        public string[] confidence = {"Not sure","Somewhat sure","Very sure"};
        [Header("Step 6 response cards")]
        [TextArea] public string[] responses = {
            "Show pictures of today's plan", "Calmly say: Not the park today. We are going to the mall.",
            "Keep asking him to walk on", "Raise your voice so he follows"
        };
        [Header("Step 8 prevention cards")]
        [TextArea] public string[] prevention = {
            "Use the picture board before leaving", "Calmly explain: Not the park today. First the mall, then home."
        };
        [Header("Reflection questions - optional VOs are below")]
        [TextArea] public string[] questions = {
            "What did you first think was happening?", "What changed your mind?", "What did your child need?"
        };
        [Header("Feedback - editable, without right/wrong labels")]
        [TextArea(4,10)] public string feedback = "Your child may have expected a park trip because getting ready looked the same as before. You knew you were going to the mall, but he had not been told. His reaction can be a sign that what happened did not match what he expected. A simple explanation or picture plan before leaving can help him understand what comes next.";
        [Header("Module")]
        public Module4VoiceCue moduleStarted = new Module4VoiceCue();
        public Module4VoiceCue participantAccepted = new Module4VoiceCue();
        public Module4VoiceCue moduleCompleted = new Module4VoiceCue();
        public Module4VoiceCue sceneLeaving = new Module4VoiceCue();
        public Module4VoiceCue sceneArrived = new Module4VoiceCue();
        [Header("Step 1 - at home")]
        public Module4VoiceCue homeStarted = new Module4VoiceCue();
        public Module4VoiceCue childGazeStarted = new Module4VoiceCue();
        public Module4VoiceCue childGazeCompleted = new Module4VoiceCue();
        public Module4VoiceCue homeObservationStarted = new Module4VoiceCue();
        public Module4VoiceCue shoesPutOn = new Module4VoiceCue();
        public Module4VoiceCue usualThingsPickedUp = new Module4VoiceCue();
        public Module4VoiceCue parentSaysLetsGo = new Module4VoiceCue();
        public Module4VoiceCue homeObservationFinished = new Module4VoiceCue();
        public Module4VoiceCue goToWalkSelected = new Module4VoiceCue();
        [Header("Step 2 - changed walk")]
        public Module4VoiceCue walkStarted = new Module4VoiceCue();
        public Module4VoiceCue walkApproachStarted = new Module4VoiceCue();
        public Module4VoiceCue walkCheckpointReached = new Module4VoiceCue();
        public Module4VoiceCue walkObservationStarted = new Module4VoiceCue();
        public Module4VoiceCue childLooksAtPark = new Module4VoiceCue();
        public Module4VoiceCue parkPassed = new Module4VoiceCue();
        public Module4VoiceCue childUpset = new Module4VoiceCue();
        public Module4VoiceCue walkObservationFinished = new Module4VoiceCue();
        [Header("Step 3 - clues")]
        public Module4VoiceCue clueStepStarted = new Module4VoiceCue();
        public Module4VoiceCue clueToolOpened = new Module4VoiceCue();
        public Module4VoiceCue clueSelected = new Module4VoiceCue();
        public Module4VoiceCue clueRemoved = new Module4VoiceCue();
        public Module4VoiceCue cluePageChanged = new Module4VoiceCue();
        public Module4VoiceCue cluesCompleted = new Module4VoiceCue();
        [Header("Step 4 - guesses")]
        public Module4VoiceCue guessStepStarted = new Module4VoiceCue();
        public Module4VoiceCue guessToolOpened = new Module4VoiceCue();
        public Module4VoiceCue guessSelected = new Module4VoiceCue();
        public Module4VoiceCue confidenceSaved = new Module4VoiceCue();
        public Module4VoiceCue guessRemoved = new Module4VoiceCue();
        public Module4VoiceCue guessPageChanged = new Module4VoiceCue();
        public Module4VoiceCue guessesCompleted = new Module4VoiceCue();
        [Header("Step 5 - home from child view")]
        public Module4VoiceCue lookBeyondOffered = new Module4VoiceCue();
        public Module4VoiceCue lookBeyondSelected = new Module4VoiceCue();
        public Module4VoiceCue childViewEntered = new Module4VoiceCue();
        public Module4VoiceCue flashbackObservationFinished = new Module4VoiceCue();
        public Module4VoiceCue returnToWalkReady = new Module4VoiceCue();
        public Module4VoiceCue returnToWalkSelected = new Module4VoiceCue();
        [Header("Step 6 - response and review")]
        public Module4VoiceCue responseStepStarted = new Module4VoiceCue();
        public Module4VoiceCue responseChoicesShown = new Module4VoiceCue();
        public Module4VoiceCue cardPictureBoard = new Module4VoiceCue();
        public Module4VoiceCue cardNotToday = new Module4VoiceCue();
        public Module4VoiceCue cardKeepWalking = new Module4VoiceCue();
        public Module4VoiceCue cardRaiseVoice = new Module4VoiceCue();
        public Module4VoiceCue responseChosen = new Module4VoiceCue();
        public Module4VoiceCue responseMidpoint = new Module4VoiceCue();
        public Module4VoiceCue responseObservationFinished = new Module4VoiceCue();
        public Module4VoiceCue guessReviewStarted = new Module4VoiceCue();
        public Module4VoiceCue guessReviewCompleted = new Module4VoiceCue();
        [Header("Step 7 - reflect")]
        public Module4VoiceCue reflectionStarted = new Module4VoiceCue();
        public Module4VoiceCue reflectionQuestion1 = new Module4VoiceCue();
        public Module4VoiceCue reflectionQuestion2 = new Module4VoiceCue();
        public Module4VoiceCue reflectionQuestion3 = new Module4VoiceCue();
        public Module4VoiceCue answerRecordingStarted = new Module4VoiceCue();
        public Module4VoiceCue answerSaved = new Module4VoiceCue();
        public Module4VoiceCue reflectionCompleted = new Module4VoiceCue();
        public Module4VoiceCue feedbackShown = new Module4VoiceCue();
        public Module4VoiceCue replayOffered = new Module4VoiceCue();
        [Header("Step 8 - replay")]
        public Module4VoiceCue replaySelected = new Module4VoiceCue();
        public Module4VoiceCue replayHomeStarted = new Module4VoiceCue();
        public Module4VoiceCue replayPictureBoard = new Module4VoiceCue();
        public Module4VoiceCue replayNotToday = new Module4VoiceCue();
        public Module4VoiceCue replayFamiliarRoutine = new Module4VoiceCue();
        public Module4VoiceCue replayExplainIfAsked = new Module4VoiceCue();
        public Module4VoiceCue expectationsSet = new Module4VoiceCue();
        public Module4VoiceCue replayWalkStarted = new Module4VoiceCue();
        public Module4VoiceCue replayParkPassed = new Module4VoiceCue();
        public Module4VoiceCue replayWalkFinished = new Module4VoiceCue();
        public Module4VoiceCue summaryShown = new Module4VoiceCue();
        public Module4VoiceCue Cue(Module4Event id) {
            switch(id) {
                case Module4Event.moduleStarted: return moduleStarted;
                case Module4Event.participantAccepted: return participantAccepted;
                case Module4Event.moduleCompleted: return moduleCompleted;
                case Module4Event.sceneLeaving: return sceneLeaving;
                case Module4Event.sceneArrived: return sceneArrived;
                case Module4Event.homeStarted: return homeStarted;
                case Module4Event.childGazeStarted: return childGazeStarted;
                case Module4Event.childGazeCompleted: return childGazeCompleted;
                case Module4Event.homeObservationStarted: return homeObservationStarted;
                case Module4Event.shoesPutOn: return shoesPutOn;
                case Module4Event.usualThingsPickedUp: return usualThingsPickedUp;
                case Module4Event.parentSaysLetsGo: return parentSaysLetsGo;
                case Module4Event.homeObservationFinished: return homeObservationFinished;
                case Module4Event.goToWalkSelected: return goToWalkSelected;
                case Module4Event.walkStarted: return walkStarted;
                case Module4Event.walkApproachStarted: return walkApproachStarted;
                case Module4Event.walkCheckpointReached: return walkCheckpointReached;
                case Module4Event.walkObservationStarted: return walkObservationStarted;
                case Module4Event.childLooksAtPark: return childLooksAtPark;
                case Module4Event.parkPassed: return parkPassed;
                case Module4Event.childUpset: return childUpset;
                case Module4Event.walkObservationFinished: return walkObservationFinished;
                case Module4Event.clueStepStarted: return clueStepStarted;
                case Module4Event.clueToolOpened: return clueToolOpened;
                case Module4Event.clueSelected: return clueSelected;
                case Module4Event.clueRemoved: return clueRemoved;
                case Module4Event.cluePageChanged: return cluePageChanged;
                case Module4Event.cluesCompleted: return cluesCompleted;
                case Module4Event.guessStepStarted: return guessStepStarted;
                case Module4Event.guessToolOpened: return guessToolOpened;
                case Module4Event.guessSelected: return guessSelected;
                case Module4Event.confidenceSaved: return confidenceSaved;
                case Module4Event.guessRemoved: return guessRemoved;
                case Module4Event.guessPageChanged: return guessPageChanged;
                case Module4Event.guessesCompleted: return guessesCompleted;
                case Module4Event.lookBeyondOffered: return lookBeyondOffered;
                case Module4Event.lookBeyondSelected: return lookBeyondSelected;
                case Module4Event.childViewEntered: return childViewEntered;
                case Module4Event.flashbackObservationFinished: return flashbackObservationFinished;
                case Module4Event.returnToWalkReady: return returnToWalkReady;
                case Module4Event.returnToWalkSelected: return returnToWalkSelected;
                case Module4Event.responseStepStarted: return responseStepStarted;
                case Module4Event.responseChoicesShown: return responseChoicesShown;
                case Module4Event.cardPictureBoard: return cardPictureBoard;
                case Module4Event.cardNotToday: return cardNotToday;
                case Module4Event.cardKeepWalking: return cardKeepWalking;
                case Module4Event.cardRaiseVoice: return cardRaiseVoice;
                case Module4Event.responseChosen: return responseChosen;
                case Module4Event.responseMidpoint: return responseMidpoint;
                case Module4Event.responseObservationFinished: return responseObservationFinished;
                case Module4Event.guessReviewStarted: return guessReviewStarted;
                case Module4Event.guessReviewCompleted: return guessReviewCompleted;
                case Module4Event.reflectionStarted: return reflectionStarted;
                case Module4Event.reflectionQuestion1: return reflectionQuestion1;
                case Module4Event.reflectionQuestion2: return reflectionQuestion2;
                case Module4Event.reflectionQuestion3: return reflectionQuestion3;
                case Module4Event.answerRecordingStarted: return answerRecordingStarted;
                case Module4Event.answerSaved: return answerSaved;
                case Module4Event.reflectionCompleted: return reflectionCompleted;
                case Module4Event.feedbackShown: return feedbackShown;
                case Module4Event.replayOffered: return replayOffered;
                case Module4Event.replaySelected: return replaySelected;
                case Module4Event.replayHomeStarted: return replayHomeStarted;
                case Module4Event.replayPictureBoard: return replayPictureBoard;
                case Module4Event.replayNotToday: return replayNotToday;
                case Module4Event.replayFamiliarRoutine: return replayFamiliarRoutine;
                case Module4Event.replayExplainIfAsked: return replayExplainIfAsked;
                case Module4Event.expectationsSet: return expectationsSet;
                case Module4Event.replayWalkStarted: return replayWalkStarted;
                case Module4Event.replayParkPassed: return replayParkPassed;
                case Module4Event.replayWalkFinished: return replayWalkFinished;
                case Module4Event.summaryShown: return summaryShown;
                default: return null;
            }
        }
        public AudioClip QuestionClip(int question) { return Cue(question==1?Module4Event.reflectionQuestion1:question==2?Module4Event.reflectionQuestion2:Module4Event.reflectionQuestion3)?.clip; }
    }
}

