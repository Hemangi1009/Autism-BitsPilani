using System;using System.Collections;using System.Text;using UnityEngine;using UnityEngine.SceneManagement;
namespace BitsPilani.Module4 {
    public sealed class Module4SceneController : MonoBehaviour {
        [Header("Scene role and authored rig start - never rewritten")]
        public bool homeScene;
        public Module4Definition definition;
        public Module4Rig rig;public Module4UI ui;public Module4Fader fader;
        [Header("Scene-authored story objects")]
        public Module4Actor child;
        public Transform childGaze,childViewPoint,parkFocus,mallFocus;
        public GameObject parentBody;
        public Module4PerspectiveBodies perspectiveBodies;
        public AudioSource childSound;
        [Tooltip("Optional child-cry sound. No sound is generated.")] public AudioClip childCrying;
        [Header("Session answer recorder - resolved on start")]
        public Module4AnswerRecorder answers;
        [Header("Walk interaction - authored park-turn checkpoint")]
        [Tooltip("Parent walks here before the changed-plan event starts. Ignored in Home.")]
        public Transform walkCheckpoint;
        [Min(.4f)] public float checkpointRadius = 1.4f;
        [Tooltip("Optional visible ground marker; hidden after the parent reaches it.")]
        public GameObject checkpointMarker;
        [Header("Authored VFX areas - reuse Module 1 arrival logic")]
        [Tooltip("First walk destination. Its saved transform is never moved.")]
        public BitsPilani.Module1.Playground.PlaygroundStandArea firstWalkArea;
        [Tooltip("Replay walk destination. Its saved transform is never moved.")]
        public BitsPilani.Module1.Playground.PlaygroundStandArea replayWalkArea;
        [Header("Walk scene - optional camera-follow object")]
        [Tooltip("Drag a scene object here. At runtime it follows the camera left/right (Y-axis rotation only), keeping its authored offset, tilt and height. Looking up/down or rolling the head does not move or tilt it. Place it where you want it in the scene first. Its parent, scale and saved scene transform are not changed. Ignored in Home; leave empty to disable.")]
        public Transform cameraFollowObject;
        [Tooltip("Hide the assigned object during horizontal walking. Restore it after stopping only if this script hid it; an initially disabled object stays disabled.")]
        public bool hideCameraObjectWhileWalking=true;
        [Min(.01f),Tooltip("Horizontal movement speed in metres/second that counts as walking. Ignores vertical motion and small tracking jitter.")]
        public float cameraObjectWalkingThreshold=.15f;
        [Min(0),Tooltip("Seconds without walking before the object reappears. Prevents flickering between walking frames.")]
        public float cameraObjectStopDelay=.25f;
        Vector3 previousFollowHeadPosition,previousFollowRootPosition;
        float previousFollowMotionTime,lastFollowWalkingTime;
        bool followMotionSampleReady,followObjectHiddenByWalking;
        Transform capturedFollowObject,capturedFollowCamera;
        Vector3 followCameraOffset;
        Quaternion followCameraRotation;
        float followCameraHeight;
        bool cameraFollowReady;
        public bool WaitingForWalkCheckpoint {get; private set;}
        Module4VideoRecording video;
        bool videoReady;
        Module4Session session;
        int cluePage,guessPage,pendingGuess=-1,gazeToken;
        bool transitioning,flashbackReady,waitingForId,initializing,responding;
        bool skippingStep;int lastSkipFrame=-1;
        public bool ReflectionVoicePending {get;private set;}
        public bool CanSkipCurrentStep => session&&videoReady&&!waitingForId&&!initializing&&!transitioning&&!session.PerspectiveTransitioning&&!skippingStep&&(!answers||!answers.IsStarting)&&CurrentStage!=Module4Stage.Summary&&CurrentStage!=Module4Stage.Complete;
        public Module4Stage CurrentStage => session?session.Data.stage:Module4Stage.HomeIntro;
        public bool IsTransitioning => transitioning;
        public bool FlashbackReady => flashbackReady;
        public bool WaitingForChildGaze {get;private set;}
        void Awake(){
            // Skip is locked until scene setup and the first fade finish.
            initializing=true;
            if(parentBody)parentBody.SetActive(false);if(firstWalkArea)firstWalkArea.Hide();if(replayWalkArea)replayWalkArea.Hide();
            var activeSession=Module4Session.Instance;
            if(homeScene&&activeSession&&activeSession.DiscardingPreparedHome){if(rig)rig.transform.root.gameObject.SetActive(false);gameObject.SetActive(false);return;}
            // Use the existing live BNG event system during the temporary Home visit.
            // Keeping two systems would leave controller pointers bound to a hidden rig.
            if(homeScene&&activeSession&&activeSession.HasSuspendedWalk&&activeSession.SharedWalkUi&&rig){
                if(rig.vrUi&&rig.vrUi!=activeSession.SharedWalkUi)rig.vrUi.gameObject.SetActive(false);
                rig.vrUi=activeSession.SharedWalkUi;
            }
        }
        IEnumerator Start(){
            session=Module4Session.Instance;
            if(session&&session.definition)definition=session.definition;
            if(!session||!definition||!rig||!ui||!child){Debug.LogError("[M4][Configuration] Assign definition, session, rig, UI and child.",this);yield break;}
            session.BindSceneVoiceEvents(GetComponent<Module4SceneVoiceEvents>());
            answers=session.GetComponent<Module4AnswerRecorder>();answers.session=session;
            video=session.GetComponent<Module4VideoRecording>();if(video)video.BindScene(rig);
            ui.Bind(this);fader.duration=definition.fadeSeconds;fader.Black();rig.ConfigureUIInput();
            rig.SetMovementLocked(homeScene);
            yield return null;
            cameraFollowReady=true;
            // Restore after BNG/emulator Start and the first tracked pose have run.
            if(!homeScene&&CurrentStage==Module4Stage.Respond&&session.hasWalkParentPose)
                yield return rig.RestoreWalkParentPose(session);
            if(!homeScene&&CurrentStage==Module4Stage.HomeIntro){yield return LoadVisit(Module4Stage.HomeIntro,definition.homeScene);yield break;}
            yield return fader.Fade(false);
            if(homeScene&&CurrentStage==Module4Stage.HomeIntro&&definition.requireParticipantId&&!Module4Session.IsValidId(session.participantId)){
                initializing=false;waitingForId=true;ui.ShowId(true);ui.Tools(false,false);session.Log("AwaitParticipantId");yield break;
            }
            ui.ShowId(false);yield return BeginVisit();
        }
        public void AcceptParticipantId(string id){
            if(!waitingForId||initializing||!session.SetParticipant(id)){ui.RefreshId();return;}
            waitingForId=false;ui.ShowId(false);session.Play(Module4Event.participantAccepted);StartCoroutine(BeginVisit());
        }
        IEnumerator BeginVisit(){
            initializing=true;
            if(video && Module4Session.IsValidId(session.participantId) && !videoReady){
                ui.Show("Preparing recording","Please wait while video recording starts.");ui.Busy();
                var start=video.EnsureStarted();while(!start.IsCompleted)yield return null;
                if(start.IsFaulted || !start.Result){
                    initializing=false;ui.Show("Recording needs attention",video.LastError,"Your ID is saved. Retry to start the module.");
                    ui.Primary("Retry recording",()=>StartCoroutine(BeginVisit()));yield break;
                }
                videoReady=true;
            }
            if(homeScene&&CurrentStage==Module4Stage.HomeIntro){
                if(!Module4Session.IsValidId(session.participantId)&&!definition.requireParticipantId)session.SetParticipant("Practice");
                ui.Step(1,"Getting ready");ui.Show("Getting ready","The scene is loading. Please get comfortable.","You can look around; movement is paused at home.");ui.Busy();
                answers.RequestPermission();
                if(definition.initialLoadSeconds>0)yield return new WaitForSecondsRealtime(definition.initialLoadSeconds);
                yield return session.Narrate(Module4Event.moduleStarted);
            }
            initializing=false;session.Play(Module4Event.sceneArrived);
            switch(CurrentStage){
                case Module4Stage.HomeIntro:StartCoroutine(HomeIntroduction());break;
                case Module4Stage.HomeFlashback:StartCoroutine(HomeFlashback());break;
                case Module4Stage.ReplayHome:StartReplayHome();break;
                case Module4Stage.WalkObserve:StartCoroutine(FirstWalk());break;
                case Module4Stage.CollectClues:BeginClues();break;
                case Module4Stage.NoteGuesses:BeginGuesses();break;
                case Module4Stage.Respond:BeginRespond();break;
                case Module4Stage.ReviewGuesses:RenderGuesses();break;
                case Module4Stage.Reflect:StartCoroutine(ReflectionQuestion());break;
                case Module4Stage.Feedback:ShowFeedback();break;
                case Module4Stage.ReplayWalk:StartCoroutine(ReplayWalk());break;
                case Module4Stage.Summary:ShowSummary();break;
                case Module4Stage.Complete:ShowCompleted();break;
            }
        }
        void Stage(Module4Stage stage,int number,string caption){session.Stage(stage);ui.Step(number,caption);ui.Tools(false,false);}
        void Caption(string text,string state){ui.body.text=text;ui.status.text=state;ui.RefreshNarrative();}
        IEnumerator HomeIntroduction(){
            Stage(Module4Stage.HomeIntro,1,"Start at home");rig.SetMovementLocked(true);child.SetBodyVisible(true);child.At(child.preparationPosition);child.Pose(Module4ChildPose.Calm);
            ui.Show("Getting ready at home","Find your child beside you.","Look at your child to begin.");
            yield return session.Narrate(Module4Event.homeStarted);
            if(definition.guidedPractice){
                if(!FactoryManager.Instance||!childGaze){ui.status.text="The scene gaze reference is missing. Assign FactoryManager and Child Gaze.";session.Log("GazeConfigurationMissing");yield break;}
                WaitingForChildGaze=true;bool seen=false;int token=++gazeToken;session.Play(Module4Event.childGazeStarted);
                // Exact MySample / Module 1 factory Action callback. The package removes its own gaze on completion.
                FactoryManager.CreateGaze(childGaze,()=>{
                    if(token!=gazeToken||CurrentStage!=Module4Stage.HomeIntro||seen)return;
                    seen=true;WaitingForChildGaze=false;session.Play(Module4Event.childGazeCompleted);session.Log("GazeCompleted","Child");
                });
                while(!seen)yield return null;
            }
            yield return HomeTimeline(definition.homeObservationSeconds,false);
            ui.Show("Ready to go out","Your child is ready to leave home.","He is calm and ready.");
            ui.Primary("Go to the Walk",GoToWalk);
            session.Play(Module4Event.homeObservationFinished);
        }
        IEnumerator HomeTimeline(float seconds,bool flashback){
            ui.Show(flashback?"From your child's side":"Watch the home routine",
                "Watch your child getting ready to leave.","Watch the static scene. Interaction is paused.");
            ui.Busy();yield return session.Narrate(Module4Event.homeObservationStarted);
            child.At(child.preparationPosition);child.Pose(Module4ChildPose.Calm);
            yield return new WaitForSecondsRealtime(seconds*.25f);
            child.Pose(Module4ChildPose.Shoes);session.Play(Module4Event.shoesPutOn);
            Caption("He puts on his shoes.","Watch your child.");
            yield return new WaitForSecondsRealtime(seconds*.30f);
            child.Pose(Module4ChildPose.Ready);session.Play(Module4Event.usualThingsPickedUp);
            Caption("He picks up his things and gets ready to leave.","Watch what happens next.");
            yield return new WaitForSecondsRealtime(seconds*.25f);
            session.Play(Module4Event.parentSaysLetsGo);
            Caption("<b>Parent:</b> “Come on, let's go.”","Observe your child.");
            child.At(child.doorwayPosition);
            yield return new WaitForSecondsRealtime(seconds*.20f);
            while(session.IsNarrating)yield return null;
        }
        public void GoToWalk(){
            if(transitioning||!homeScene)return;
            if(CurrentStage==Module4Stage.HomeIntro){session.Play(Module4Event.goToWalkSelected);StartCoroutine(LoadVisit(Module4Stage.WalkObserve,definition.walkScene));}
            else if(CurrentStage==Module4Stage.ReplayHome&&session.Data.expectationsSet){session.Play(Module4Event.goToWalkSelected);StartCoroutine(LoadVisit(Module4Stage.ReplayWalk,definition.walkScene));}
        }
        // Walk uses the child placement authored in the scene. Do not snap to the legacy walk marker.
        IEnumerator FirstWalk(){
            Stage(Module4Stage.WalkObserve,2,"Walk towards the park");rig.SetMovementLocked(false);
            yield return WalkToCheckpoint(false);child.Pose(Module4ChildPose.Ready);
            ui.Show("Watch what changes","Watch your child as you continue along the path.","You can move around to observe your child.");ui.Busy();
            yield return session.Narrate(Module4Event.walkStarted);yield return session.Narrate(Module4Event.walkObservationStarted);
            float t=definition.walkObservationSeconds;
            yield return new WaitForSecondsRealtime(t*.30f);child.Pose(Module4ChildPose.ExpectingPark);FaceChildTo(parkFocus);session.Play(Module4Event.childLooksAtPark);
            Caption("He looks towards the park.","Keep observing your child.");
            yield return new WaitForSecondsRealtime(t*.30f);session.Play(Module4Event.parkPassed);
            Caption("You continue past the park entrance.","Watch what happens next.");
            yield return new WaitForSecondsRealtime(t*.15f);child.Pose(Module4ChildPose.Upset);Cry(true);session.Play(Module4Event.childUpset);
            Caption("He stops, cries and does not use words. His body looks tense.","The scene holds so you can look closely.");
            yield return new WaitForSecondsRealtime(t*.25f);while(session.IsNarrating)yield return null;
            session.Play(Module4Event.walkObservationFinished);rig.SetMovementLocked(false);
            ui.Primary("Collect clues",BeginClues);
        }
        IEnumerator WalkToCheckpoint(bool replay){
            rig.SetMovementLocked(false);
            if(!walkCheckpoint){session.Log("CheckpointMissing","Walk checkpoint not assigned; continuing safely.");yield break;}
            var area=replay?replayWalkArea:firstWalkArea;
            if(firstWalkArea)firstWalkArea.Hide();if(replayWalkArea)replayWalkArea.Hide();
            if(area){walkCheckpoint=area.transform;area.Show();}
            WaitingForWalkCheckpoint=true;if(checkpointMarker)checkpointMarker.SetActive(!area);
            child.Pose(Module4ChildPose.Ready);
            ui.Show(replay?"Try the walk again":"Walk with your child towards the park",
                "Follow the pavement to the marked area beside the park turn. Look at your child and the two possible directions.",
                "Walk into the marked area to continue. You can move freely.");ui.Busy();
            session.Play(Module4Event.walkApproachStarted);
            if(replay){
                // Replay advances on first contact, without requiring the player to stay.
                // Keep Module 1 and the first-walk VFX timing unchanged.
                while(!rig.IsAt(walkCheckpoint,checkpointRadius))yield return null;
                if(area)area.Hide();
                session.Log("ReplayCheckpointTouched","Showing conclusion immediately");
            }else if(area){
                // The same Show / AdvanceArrival contract as Module 1, including
                // players already inside when the VFX becomes visible.
                while(!area.AdvanceArrival(Time.unscaledTime,rig.IsAt(area.transform,checkpointRadius)))yield return null;
            }else{
                while(!rig.IsAt(walkCheckpoint,checkpointRadius))yield return null;
                yield return new WaitForSecondsRealtime(1f);
            }
            WaitingForWalkCheckpoint=false;if(checkpointMarker)checkpointMarker.SetActive(false);
            session.Play(Module4Event.walkCheckpointReached);session.Log("ParkTurnReached",replay?"Replay":"First walk");
        }
        void FaceChildTo(Transform point){if(!point||!child.head)return;Vector3 d=point.position-child.head.position;d.y=0;if(d.sqrMagnitude>.01f)child.head.rotation=Quaternion.LookRotation(d);}
        void Cry(bool enabled){if(!childSound)return;if(enabled&&childCrying){childSound.clip=childCrying;childSound.loop=true;if(!childSound.isPlaying)childSound.Play();}else if(!enabled)childSound.Stop();}
        public void BeginClues(){if(transitioning)return;Stage(Module4Stage.CollectClues,3,"Collect clues");session.PrepareHomePerspective();session.Play(Module4Event.clueStepStarted);OpenClueTool();}
        public void OpenClueTool(){if(CurrentStage!=Module4Stage.CollectClues)return;RenderClues();session.Play(Module4Event.clueToolOpened);}
        void RenderClues(){
            ui.Show("Collect clues","<b>Select at least 3 clues.</b>\n\nLook at your child, the park, the mall and the path. Select a card again to remove it.","Selected: "+session.Data.clues.Count,true);
            bool[] selected=new bool[definition.clues.Length];for(int i=0;i<selected.Length;i++)selected[i]=session.Data.clues.Contains(definition.clues[i]);
            Cards(definition.clues,selected,cluePage,SelectClue);ui.Tools(true,false);
            ui.footer.text="PAGE "+(cluePage+1)+" OF "+Pages(definition.clues.Length)+"   |   "+session.Data.clues.Count+" clues selected";
            ui.Primary("Done",CompleteClues,session.Data.clues.Count>=3,session.Data.clues.Count>=3);
            ui.Secondary("More clues",()=>{cluePage=(cluePage+1)%Pages(definition.clues.Length);RenderClues();session.Play(Module4Event.cluePageChanged);},Pages(definition.clues.Length)>1,Pages(definition.clues.Length)>1);
        }
        public void SelectClue(int index){if(CurrentStage!=Module4Stage.CollectClues||index<0||index>=definition.clues.Length)return;string c=definition.clues[index];bool remove=session.Data.clues.Contains(c);if(remove)session.Data.clues.Remove(c);else session.Data.clues.Add(c);session.Log(remove?"ClueRemoved":"ClueSelected",c);RenderClues();session.Play(remove?Module4Event.clueRemoved:Module4Event.clueSelected);}
        public void CompleteClues(){if(CurrentStage!=Module4Stage.CollectClues||session.Data.clues.Count<3)return;session.Play(Module4Event.cluesCompleted);BeginGuesses();}
        public void BeginGuesses(){Stage(Module4Stage.NoteGuesses,4,"Note your guesses");session.Play(Module4Event.guessStepStarted);OpenGuessTool();}
        public void OpenGuessTool(){if(CurrentStage!=Module4Stage.NoteGuesses&&CurrentStage!=Module4Stage.ReviewGuesses)return;RenderGuesses();session.Play(Module4Event.guessToolOpened);}
        string ReviewContext(){
            var b=new StringBuilder("<b>Clues you selected</b>\n");if(session.Data.clues.Count==0)b.AppendLine(ui.noCluesText);for(int i=0;i<session.Data.clues.Count;i++)b.AppendLine((i+1)+". "+session.Data.clues[i]);
            b.Append("\n<b>Your action</b>\n").AppendLine(session.Data.firstResponse).Append("\n<b>Child's response</b>\n").Append(session.Data.firstOutcome);return b.ToString();
        }
        void RenderGuesses(){
            pendingGuess=-1;bool review=CurrentStage==Module4Stage.ReviewGuesses;ui.Step(review?6:4,review?"Review your guesses":"Note your guesses");
            ui.Show(review?"Review your guesses":"What might be going on?",review?ReviewContext():"<b>Save at least 1 possible idea.</b>\n\nChoose a guess, then how sure you are. You can keep more than one idea.",
                review?"Choose a guess to change your confidence.":"Saved ideas: "+session.Data.guesses.Count,true);
            bool[] selected=new bool[definition.guesses.Length];for(int i=0;i<selected.Length;i++)selected[i]=FindGuess(definition.guesses[i])!=null;
            Cards(definition.guesses,selected,guessPage,SelectGuess);ui.Tools(false,true);if(review)ui.FormatGuessReviewText();
            ui.footer.text="PAGE "+(guessPage+1)+" OF "+Pages(definition.guesses.Length)+"   |   "+session.Data.guesses.Count+" saved ideas";
            ui.Primary("Done",review?(UnityEngine.Events.UnityAction)CompleteGuessReview:CompleteGuesses,session.Data.guesses.Count>=1);
            ui.Secondary("More ideas",()=>{guessPage=(guessPage+1)%Pages(definition.guesses.Length);RenderGuesses();session.Play(Module4Event.guessPageChanged);},Pages(definition.guesses.Length)>1,Pages(definition.guesses.Length)>1);
        }
        public void SelectGuess(int index){
            if((CurrentStage!=Module4Stage.NoteGuesses&&CurrentStage!=Module4Stage.ReviewGuesses)||index<0||index>=definition.guesses.Length)return;
            pendingGuess=index;guessPage=index/ui.choices.Length;var existing=FindGuess(definition.guesses[index]);
            ui.Show("How sure are you?",definition.guesses[index],"Select the saved confidence again to remove this idea.",true);
            bool[] selected={existing!=null&&existing.confidence==0,existing!=null&&existing.confidence==1,existing!=null&&existing.confidence==2};
            ui.Draw(definition.confidence,selected,SetGuessConfidence);ui.Secondary("Back to ideas",OpenGuessTool);session.Play(Module4Event.guessSelected);
        }
        public void SetGuessConfidence(int confidence){
            if(pendingGuess<0||confidence<0||confidence>2)return;
            string card=definition.guesses[pendingGuess];var g=FindGuess(card);
            if(g!=null&&g.confidence==confidence){session.Data.guesses.Remove(g);session.Log("GuessRemoved",card);RenderGuesses();session.Play(Module4Event.guessRemoved);return;}
            if(g==null){g=new Module4Guess{idea=card};session.Data.guesses.Add(g);}g.confidence=confidence;session.Log("ConfidenceSaved",card+" | "+definition.confidence[confidence]);RenderGuesses();session.Play(Module4Event.confidenceSaved);
        }
        Module4Guess FindGuess(string idea){return session.Data.guesses.Find(g=>g.idea==idea);}
        int Pages(int count){return Mathf.CeilToInt((float)count/Mathf.Max(1,ui.choices.Length));}
        void Cards(string[] source,bool[] selected,int page,Action<int> callback){int start=page*ui.choices.Length,count=Mathf.Min(ui.choices.Length,source.Length-start);var labels=new string[count];var marked=new bool[count];Array.Copy(source,start,labels,0,count);Array.Copy(selected,start,marked,0,count);ui.Draw(labels,marked,i=>callback(start+i));}
        public void CompleteGuesses(){if(CurrentStage!=Module4Stage.NoteGuesses||pendingGuess>=0||session.Data.guesses.Count<1)return;session.Play(Module4Event.guessesCompleted);OfferHomePerspective();}
        void OfferHomePerspective(){ui.Step(5,"Look beyond the scene");session.Stage(Module4Stage.HomeFlashback);ui.Tools(false,false);ui.Show("Look beyond the scene","View the scene at home from your child's perspective.","Select Look Beyond the Scene to continue.");ui.Primary("Look Beyond the Scene",LookBeyond);session.Play(Module4Event.lookBeyondOffered);}
        public void LookBeyond(){if(transitioning||CurrentStage!=Module4Stage.HomeFlashback||homeScene)return;session.Play(Module4Event.lookBeyondSelected);StartCoroutine(LoadVisit(Module4Stage.HomeFlashback,definition.homeScene));}
        IEnumerator HomeFlashback(){
            Stage(Module4Stage.HomeFlashback,5,"From your child's side");rig.SetMovementLocked(true);child.At(child.preparationPosition);
            if(perspectiveBodies)perspectiveBodies.EnterChildView(rig);else if(parentBody)parentBody.SetActive(true);
            if(definition.childEyeView){rig.EnterChildView(childViewPoint,definition.childEyeHeight);child.SetBodyVisible(false);}
            flashbackReady=false;ui.SetChildReturn(false);
            // Show the current step immediately, including while its entry VO is playing.
            ui.Show("From your child's side","Watch your child getting ready to leave.","Watch the static scene. Interaction is paused.");ui.Busy();
            yield return session.Narrate(Module4Event.childViewEntered);yield return HomeTimeline(definition.flashbackSeconds,true);
            flashbackReady=true;ui.Show("Review what you noticed","You can now return to the walk.","Select Return to the Walk when ready.");
            ui.Primary("Return to the Walk",ReturnToWalk);ui.SetChildReturn(true);session.Play(Module4Event.flashbackObservationFinished);session.Play(Module4Event.returnToWalkReady);
        }
        public void ReturnToWalk(){if(!homeScene||CurrentStage!=Module4Stage.HomeFlashback||!flashbackReady||transitioning)return;ui.SetChildReturn(false);session.Play(Module4Event.returnToWalkSelected);StartCoroutine(LoadVisit(Module4Stage.Respond,definition.walkScene));}
        public void BeginRespond(){Stage(Module4Stage.Respond,6,"Respond to your child");rig.SetMovementLocked(false);child.Pose(Module4ChildPose.Upset);Cry(true);ui.Show("How will you respond?","Your child is still upset. Choose a response and watch what happens.","Select one response card.",true);ui.Draw(definition.responses,null,ChooseResponse);session.Play(Module4Event.responseStepStarted);session.Play(Module4Event.responseChoicesShown);}
        public void ChooseResponse(int index){if(CurrentStage!=Module4Stage.Respond||responding||index<0||index>=definition.responses.Length)return;responding=true;StartCoroutine(Response(index));}
        IEnumerator Response(int index){
            ui.Show("Watch your child's response",definition.responses[index],"Interaction is paused while you watch.");ui.Busy();rig.SetMovementLocked(homeScene);
            session.Data.firstResponse=definition.responses[index];session.Data.firstResponseIndex=index;session.Log("ResponseSelected",definition.responses[index]);
            yield return session.Narrate(index==0?Module4Event.cardPictureBoard:index==1?Module4Event.cardNotToday:index==2?Module4Event.cardKeepWalking:Module4Event.cardRaiseVoice);
            if(index==0)child.ShowBoard(true);if(definition.responseDialoguePauseSeconds>0)yield return new WaitForSecondsRealtime(definition.responseDialoguePauseSeconds);
            yield return session.Narrate(Module4Event.responseChosen);
            yield return new WaitForSecondsRealtime(definition.responseObservationSeconds*.5f);
            bool calm=index<2;child.Pose(calm?Module4ChildPose.Settling:Module4ChildPose.MoreUpset);if(index==0)child.ShowBoard(true);if(calm)Cry(false);session.Play(Module4Event.responseMidpoint);
            Caption(calm?"He starts to settle.":"He continues crying and appears more upset.","Keep watching his response.");
            yield return new WaitForSecondsRealtime(definition.responseObservationSeconds*.5f);while(session.IsNarrating)yield return null;
            child.Pose(calm?Module4ChildPose.Calmer:Module4ChildPose.MoreUpset);session.Data.firstOutcome=calm?"He began to settle during the observation.":"He remained upset during the observation.";
            session.Data.firstResponseSeconds=definition.responseObservationSeconds;session.Play(Module4Event.responseObservationFinished);responding=false;rig.SetMovementLocked(false);
            Stage(Module4Stage.ReviewGuesses,6,"Review your guesses");session.Play(Module4Event.guessReviewStarted);RenderGuesses();
        }
        public void CompleteGuessReview(){if(CurrentStage!=Module4Stage.ReviewGuesses||pendingGuess>=0||session.Data.guesses.Count<1)return;session.Play(Module4Event.guessReviewCompleted);session.Stage(Module4Stage.Reflect);session.Data.reflectionQuestion=1;StartCoroutine(BeginReflection());}
        IEnumerator BeginReflection(){yield return PresentReflectionQuestion(true);}
        IEnumerator ReflectionQuestion(){yield return PresentReflectionQuestion(false);}
        IEnumerator PresentReflectionQuestion(bool introduction){
            ReflectionVoicePending=true;ui.Step(7,"Reflect");ui.Tools(false,false);
            int q=Mathf.Clamp(session.Data.reflectionQuestion,1,3);ui.Reflection(definition.questions[q-1],q);
            Canvas.ForceUpdateCanvases();
            // Render the recording panel before starting either VO. Keep Record/Next
            // gated throughout the introduction and question, including empty clips.
            yield return null;
            if(introduction)yield return session.Narrate(Module4Event.reflectionStarted);
            yield return session.Narrate(q==1?Module4Event.reflectionQuestion1:q==2?Module4Event.reflectionQuestion2:Module4Event.reflectionQuestion3);
            ReflectionVoicePending=false;ui.RefreshRecording();
        }
        public void ToggleAnswerRecording(){if(CurrentStage!=Module4Stage.Reflect||ReflectionVoicePending||session.IsNarrating||answers.IsStarting)return;answers.Toggle(session.Data.reflectionQuestion);ui.RefreshRecording();}
        public void NextReflectionQuestion(){
            int q=session.Data.reflectionQuestion;if(CurrentStage!=Module4Stage.Reflect||ReflectionVoicePending||session.IsNarrating||answers.IsRecording||answers.IsStarting||(definition.requireSavedAnswer&&!session.HasAnswer(q)))return;
            if(q<3){session.Data.reflectionQuestion++;session.Save();StartCoroutine(ReflectionQuestion());}else{session.Play(Module4Event.reflectionCompleted);session.Stage(Module4Stage.Feedback);ShowFeedback();}
        }
        public void ShowFeedback(){
            ui.Step(7,"Reflect");ui.AttemptReview(session.Data,definition);
            ui.Primary("Replay and prevent",Replay);session.Play(Module4Event.feedbackShown);session.Play(Module4Event.replayOffered);
        }
        public void Replay(){if(CurrentStage!=Module4Stage.Feedback||transitioning||session.Data.replayUsed)return;session.Data.replayUsed=true;session.Play(Module4Event.replaySelected);StartCoroutine(LoadVisit(Module4Stage.ReplayHome,definition.homeScene));}
        void StartReplayHome(){
            Stage(Module4Stage.ReplayHome,8,"Replay and prevent");rig.SetMovementLocked(true);child.SetBodyVisible(true);child.At(child.preparationPosition);child.Pose(Module4ChildPose.Calm);session.Data.expectationsSet=false;
            ui.Show("Choose your response before leaving","Choose an approach for this attempt.","Select one response card.",true);ui.Draw(definition.prevention,null,ChoosePrevention);session.Play(Module4Event.replayHomeStarted);
        }
        public void ChoosePrevention(int index){if(CurrentStage!=Module4Stage.ReplayHome||transitioning||responding||index<0||index>=definition.prevention.Length)return;responding=true;StartCoroutine(Prevention(index));}
        IEnumerator Prevention(int index){
            ui.Busy();yield return session.Narrate(index==0?Module4Event.replayPictureBoard:index==1?Module4Event.replayNotToday:index==2?Module4Event.replayFamiliarRoutine:Module4Event.replayExplainIfAsked);
            session.Data.replayResponse=definition.prevention[index];session.Data.expectationsSet=true;session.Data.replayPlanExplained=index<2;session.Log("ExpectationsSet",session.Data.replayResponse);child.Pose(Module4ChildPose.Ready);child.ShowBoard(index==0);
            ui.Show(session.Data.replayPlanExplained?"Your choice is saved":"Try your chosen approach",session.Data.replayPlanExplained?"Your choice has been saved. Continue to the walk.":"Your choice has been saved. Continue to the walk.",session.Data.replayPlanExplained?"Observe your child during the next walk.":"Watch his response when you reach the park turn.");ui.Primary("Go to the Walk",GoToWalk);responding=false;session.Play(Module4Event.expectationsSet);
        }
        IEnumerator ReplayWalk(){
            Stage(Module4Stage.ReplayWalk,8,"Try the walk again");rig.SetMovementLocked(false);
            yield return WalkToCheckpoint(true);
            Cry(false);
            // The replay outcome and conclusion are committed on arrival. Optional VO
            // events remain available, but no timer or narration queue delays the UI.
            session.Play(Module4Event.replayWalkStarted);
            session.Play(Module4Event.replayParkPassed);
            child.Pose(session.Data.replayPlanExplained?Module4ChildPose.Calmer:Module4ChildPose.Upset);
            session.Data.replayOutcome=session.Data.replayPlanExplained?"He stayed calmer during the replay.":"He stopped and cried near the park turn.";
            session.Play(Module4Event.replayWalkFinished);session.Stage(Module4Stage.Summary);
            rig.SetMovementLocked(false);ShowSummary();session.Log("ReplayConclusionShown","Checkpoint reached; no observation wait");
        }
        public void ShowSummary(){ui.Tools(false,false);ui.Summary(session.Data,definition);ui.Primary("Complete module",Complete);session.Play(Module4Event.summaryShown);}
        public void Complete(){if(CurrentStage!=Module4Stage.Summary||transitioning)return;StartCoroutine(FinishModule());}
        IEnumerator FinishModule(){
            // Lock immediately so repeated presses cannot start multiple save/load operations.
            transitioning=true;ui.Busy();
            if(video){var saving=video.StopAndSave("Module completed");while(!saving.IsCompleted)yield return null;}
            session.Data.completed=true;session.Stage(Module4Stage.Complete);
            session.Play(Module4Event.moduleCompleted);session.Save();
            session.Log("ModuleCompletedReturningToEntry");
            transitioning=false;yield return LoadVisit(Module4Stage.Complete,"Autism_ModuleEntry");
        }
        // A completed session always returns directly; no second confirmation panel.
        void ShowCompleted(){ReturnToEntry();}
        public void ReturnToEntry(){if(transitioning||CurrentStage!=Module4Stage.Complete)return;StartCoroutine(LoadVisit(Module4Stage.Complete,"Autism_ModuleEntry"));}
        IEnumerator LoadVisit(Module4Stage target,string sceneName){
            if(transitioning)yield break;transitioning=true;ui.Busy();ui.SetChildReturn(false);rig.SetMovementLocked(true);gazeToken++;
            if(!homeScene&&target==Module4Stage.HomeFlashback)rig.CaptureWalkParentPose(session);
            session.Play(Module4Event.sceneLeaving);while(session.IsNarrating)yield return null;
            if(answers&&answers.IsRecording)answers.StopAndSave();
            yield return fader.Fade(true);
            if(!Application.CanStreamedLevelBeLoaded(sceneName)){session.Log("SceneLoadFailed",sceneName);transitioning=false;yield return fader.Fade(false);ui.Show("Scene unavailable","Add "+sceneName+" to this build's scene list.","Your session has been kept.");yield break;}
            if(rig.InChildView)rig.ReturnToParentView();if(perspectiveBodies)perspectiveBodies.RestoreParentView();session.Stage(target);session.Save();
            if(sceneName!=definition.homeScene&&sceneName!=definition.walkScene&&video){var saving=video.StopAndSave("Leaving Module 4");while(!saving.IsCompleted)yield return null;}
            if(!homeScene&&target==Module4Stage.HomeFlashback){
                session.StartCoroutine(session.OpenHomePerspective(this));yield break;
            }
            if(homeScene&&target==Module4Stage.Respond&&session.HasSuspendedWalk){
                session.StartCoroutine(session.ResumeWalkPerspective(this));yield break;
            }
            yield return session.DiscardPreparedPerspective();
            if(video)video.SuspendSceneCapture();
            var operation=SceneManager.LoadSceneAsync(sceneName,LoadSceneMode.Single);while(operation!=null&&!operation.isDone)yield return null;
        }
        public IEnumerator ResumeAfterHomePerspective(){
            session.BindSceneVoiceEvents(GetComponent<Module4SceneVoiceEvents>());
            transitioning=true;ui.SetChildReturn(false);fader.Black();rig.SetMovementLocked(true);
            yield return rig.RestoreWalkParentPose(session);
            if(video)video.BindScene(rig);rig.ConfigureUIInput();
            yield return fader.Fade(false);BeginRespond();
            transitioning=false;
            session.Log("ParentViewRestored",rig.head.position.ToString("F3"));
        }
        public void SkipCurrentStep(){
            if(!CanSkipCurrentStep||lastSkipFrame==Time.frameCount)return;
            lastSkipFrame=Time.frameCount;var skipped=CurrentStage;skippingStep=true;
            StopAllCoroutines();gazeToken++;WaitingForChildGaze=false;WaitingForWalkCheckpoint=false;pendingGuess=-1;responding=false;ReflectionVoicePending=false;
            session.CompleteStepCallbacksOnSkip(skipped,homeScene);if(answers&&answers.IsRecording)answers.StopAndSave(false);
            // Coroutine cancellation must not leave an invisible fade blocking UI rays.
            fader.Clear();ui.Busy();Cry(false);
            if(firstWalkArea)firstWalkArea.Hide();if(replayWalkArea)replayWalkArea.Hide();if(checkpointMarker)checkpointMarker.SetActive(false);
            if(childGaze)foreach(var gaze in childGaze.GetComponentsInChildren<global::GazeButton>(true))Destroy(gaze.gameObject);
            ui.SetChildReturn(false);ui.Tools(false,false);rig.SetMovementLocked(homeScene);
            if(session.Data.skippedSteps==null)session.Data.skippedSteps=new System.Collections.Generic.List<string>();
            if(!session.Data.skippedSteps.Contains(skipped.ToString()))session.Data.skippedSteps.Add(skipped.ToString());
            string detail="Whole step skipped";
            if(skipped==Module4Stage.CollectClues){session.Data.clues.Clear();detail=ui.noCluesText;}
            if(skipped==Module4Stage.NoteGuesses){session.Data.guesses.Clear();detail=ui.noGuessesText;}
            session.Log("StepSkipped",skipped+" | "+detail);
            StartCoroutine(AdvanceSkippedStep(skipped));
        }
        IEnumerator AdvanceSkippedStep(Module4Stage skipped){
            switch(skipped){
                case Module4Stage.HomeIntro:skippingStep=false;GoToWalk();yield break;
                case Module4Stage.WalkObserve:child.Pose(Module4ChildPose.Upset);BeginClues();break;
                case Module4Stage.CollectClues:BeginGuesses();ui.footer.text=ui.noCluesText;break;
                case Module4Stage.NoteGuesses:OfferHomePerspective();ui.footer.text=ui.noGuessesText;break;
                case Module4Stage.HomeFlashback:
                    if(homeScene){flashbackReady=true;skippingStep=false;ReturnToWalk();yield break;}
                    ui.Show("Moving to the next step","Please wait.");ui.Busy();
                    yield return session.DiscardPreparedPerspective();
                    // Home Awake assigned its caster to all loaded canvases. Rebind after unload.
                    rig.ConfigureUIInput();fader.Clear();BeginRespond();break;
                case Module4Stage.Respond:
                case Module4Stage.ReviewGuesses:
                    if(skipped==Module4Stage.Respond)session.Data.firstOutcome="Not observed (step skipped)";
                    session.Stage(Module4Stage.Reflect);session.Data.reflectionQuestion=1;StartCoroutine(BeginReflection());break;
                case Module4Stage.Reflect:session.Log("ReflectionSkipped","Unanswered questions were not recorded");session.Stage(Module4Stage.Feedback);skippingStep=false;Replay();yield break;
                case Module4Stage.Feedback:skippingStep=false;Replay();yield break;
                case Module4Stage.ReplayHome:
                case Module4Stage.ReplayWalk:
                    session.Data.replayUsed=true;session.Data.replayOutcome="Not observed (step skipped)";session.Stage(Module4Stage.Summary);ShowSummary();break;
            }
            rig.ConfigureUIInput();
            skippingStep=false;session.Log("SkipCompleted",CurrentStage+" | panel="+ui.title.text);
        }
        void OnEnable(){Application.onBeforeRender+=UpdateCameraFollowPose;}
        void OnDisable(){Application.onBeforeRender-=UpdateCameraFollowPose;}
        void LateUpdate(){
            UpdateCameraFollowPose();
            if(!homeScene&&cameraFollowReady&&cameraFollowObject&&rig&&rig.head&&cameraFollowObject!=rig.head&&!rig.head.IsChildOf(cameraFollowObject)&&!transform.IsChildOf(cameraFollowObject))
                UpdateCameraObjectWalkingVisibility(rig.head);
        }
        void UpdateCameraFollowPose(){
            if(homeScene||!cameraFollowReady||!cameraFollowObject||!rig||!rig.head)return;
            Transform camera=rig.head;
            Quaternion yaw=Quaternion.Euler(0,camera.eulerAngles.y,0);
            if(capturedFollowObject!=cameraFollowObject||capturedFollowCamera!=camera){
                // Release only visibility changes owned by the walking feature.
                if(capturedFollowObject&&followObjectHiddenByWalking)capturedFollowObject.gameObject.SetActive(true);
                followObjectHiddenByWalking=false;followMotionSampleReady=false;
                capturedFollowObject=cameraFollowObject;capturedFollowCamera=camera;
                // Reject anything that would move the camera itself and create a feedback loop.
                if(cameraFollowObject==camera||camera.IsChildOf(cameraFollowObject)||transform.IsChildOf(cameraFollowObject)){
                    Debug.LogWarning("[M4][CameraFollow] Assign an independent scene object, not the camera, its rig, or the flow-controller parent.",this);
                    return;
                }
                // Capture the scene placement once; never impose a hard-coded offset.
                // Camera children have an authored local offset. Strip inherited pitch/roll.
                Quaternion basis=cameraFollowObject.IsChildOf(camera)?camera.rotation:yaw;
                followCameraOffset=Quaternion.Inverse(basis)*(cameraFollowObject.position-camera.position);
                followCameraRotation=Quaternion.Inverse(basis)*cameraFollowObject.rotation;
                followCameraHeight=camera.position.y;
                Debug.Log("[M4][CameraFollow] Following "+cameraFollowObject.name+" at its authored camera-relative placement.",this);
            }
            if(cameraFollowObject==camera||camera.IsChildOf(cameraFollowObject)||transform.IsChildOf(cameraFollowObject))return;
            Vector3 origin=camera.position;origin.y=followCameraHeight;
            cameraFollowObject.SetPositionAndRotation(origin+yaw*followCameraOffset,yaw*followCameraRotation);
        }
        void UpdateCameraObjectWalkingVisibility(Transform camera){
            float now=Time.unscaledTime;
            Vector3 rootPosition=rig.playerRoot?rig.playerRoot.position:camera.position;
            float elapsed=now-previousFollowMotionTime;
            bool walking=false;
            // Re-prime after suspension/scene transitions instead of treating a restored pose as walking.
            if(followMotionSampleReady&&elapsed>0&&elapsed<.25f){
                Vector3 headDelta=camera.position-previousFollowHeadPosition;headDelta.y=0;
                Vector3 rootDelta=rootPosition-previousFollowRootPosition;rootDelta.y=0;
                // Snap/smooth turning can move the rig pivot while the head stays in place.
                // Require horizontal travel of both, so turning alone does not hide the object.
                float speed=Mathf.Min(headDelta.magnitude,rootDelta.magnitude)/elapsed;
                walking=speed>=Mathf.Max(.01f,cameraObjectWalkingThreshold);
            }
            previousFollowHeadPosition=camera.position;previousFollowRootPosition=rootPosition;
            previousFollowMotionTime=now;
            if(!followMotionSampleReady)lastFollowWalkingTime=now-Mathf.Max(0,cameraObjectStopDelay);
            followMotionSampleReady=true;
            if(walking)lastFollowWalkingTime=now;
            bool hide=hideCameraObjectWhileWalking&&(walking||now-lastFollowWalkingTime<Mathf.Max(0,cameraObjectStopDelay));
            if(hide&&!followObjectHiddenByWalking&&cameraFollowObject.gameObject.activeSelf){
                followObjectHiddenByWalking=true;cameraFollowObject.gameObject.SetActive(false);
                Debug.Log("[M4][CameraFollow] Hidden while walking.",this);
            }else if(!hide&&followObjectHiddenByWalking){
                followObjectHiddenByWalking=false;cameraFollowObject.gameObject.SetActive(true);
                Debug.Log("[M4][CameraFollow] Shown after walking stopped.",this);
            }
        }
        void OnDestroy(){gazeToken++;if(childSound)childSound.Stop();}
    }
}

