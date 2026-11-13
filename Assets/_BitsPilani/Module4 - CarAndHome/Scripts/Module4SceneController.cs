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
        public bool WaitingForWalkCheckpoint {get; private set;}
        Module4VideoRecording video;
        bool videoReady;
        Module4Session session;
        int cluePage,guessPage,pendingGuess=-1,gazeToken;
        bool transitioning,flashbackReady,waitingForId,initializing,responding;
        public Module4Stage CurrentStage => session?session.Data.stage:Module4Stage.HomeIntro;
        public bool IsTransitioning => transitioning;
        public bool FlashbackReady => flashbackReady;
        public bool WaitingForChildGaze {get;private set;}
        void Awake(){
            if(parentBody)parentBody.SetActive(false);if(firstWalkArea)firstWalkArea.Hide();if(replayWalkArea)replayWalkArea.Hide();
            var activeSession=Module4Session.Instance;
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
            answers=session.GetComponent<Module4AnswerRecorder>();answers.session=session;
            video=session.GetComponent<Module4VideoRecording>();if(video)video.BindScene(rig);
            ui.Bind(this);fader.duration=definition.fadeSeconds;fader.Black();rig.ConfigureUIInput();
            rig.SetMovementLocked(homeScene);
            yield return null;
            // Restore after BNG/emulator Start and the first tracked pose have run.
            if(!homeScene&&CurrentStage==Module4Stage.Respond&&session.hasWalkParentPose)
                yield return rig.RestoreWalkParentPose(session);
            if(!homeScene&&CurrentStage==Module4Stage.HomeIntro){yield return LoadVisit(Module4Stage.HomeIntro,definition.homeScene);yield break;}
            yield return fader.Fade(false);
            if(homeScene&&CurrentStage==Module4Stage.HomeIntro&&definition.requireParticipantId&&!Module4Session.IsValidId(session.participantId)){
                waitingForId=true;ui.ShowId(true);ui.Tools(false,false);session.Log("AwaitParticipantId");yield break;
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
            ui.Show("Getting ready at home","Find your child beside you. Notice how getting ready looks familiar.","Look at your child to begin.");
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
            ui.Show("Ready to go out","Your child followed his usual getting-ready routine. No destination was explained.","He is calm and ready.");
            ui.Primary("Go to the Walk",GoToWalk);
            session.Play(Module4Event.homeObservationFinished);
        }
        IEnumerator HomeTimeline(float seconds,bool flashback){
            ui.Show(flashback?"From your child's side":"Watch the home routine",
                "Your child is getting ready in the same way as before a park trip.","Watch the static scene. Interaction is paused.");
            ui.Busy();yield return session.Narrate(Module4Event.homeObservationStarted);
            child.At(child.preparationPosition);child.Pose(Module4ChildPose.Calm);
            yield return new WaitForSecondsRealtime(seconds*.25f);
            child.Pose(Module4ChildPose.Shoes);session.Play(Module4Event.shoesPutOn);
            Caption("He puts on his shoes. This is the familiar start of going out.","A familiar routine.");
            yield return new WaitForSecondsRealtime(seconds*.30f);
            child.Pose(Module4ChildPose.Ready);session.Play(Module4Event.usualThingsPickedUp);
            Caption("He picks up his usual things and gets ready to leave.","He has not been told where you are going.");
            yield return new WaitForSecondsRealtime(seconds*.25f);
            session.Play(Module4Event.parentSaysLetsGo);
            Caption("<b>Parent:</b> “Come on, let's go.”\n\nThe destination has not been explained.","He may expect the usual park trip.");
            child.At(child.doorwayPosition);
            yield return new WaitForSecondsRealtime(seconds*.20f);
            while(session.IsNarrating)yield return null;
        }
        public void GoToWalk(){
            if(transitioning||!homeScene)return;
            if(CurrentStage==Module4Stage.HomeIntro){session.Play(Module4Event.goToWalkSelected);StartCoroutine(LoadVisit(Module4Stage.WalkObserve,definition.walkScene));}
            else if(CurrentStage==Module4Stage.ReplayHome&&session.Data.expectationsSet){session.Play(Module4Event.goToWalkSelected);StartCoroutine(LoadVisit(Module4Stage.ReplayWalk,definition.walkScene));}
        }
        IEnumerator FirstWalk(){
            Stage(Module4Stage.WalkObserve,2,"Walk towards the park");rig.SetMovementLocked(false);
            yield return WalkToCheckpoint(false);child.At(child.walkPosition);child.Pose(Module4ChildPose.Ready);
            ui.Show("Watch what changes","You planned to go to the mall. Your child has not been told that the plan is different.","You can move around to observe your child.");ui.Busy();
            yield return session.Narrate(Module4Event.walkStarted);yield return session.Narrate(Module4Event.walkObservationStarted);
            float t=definition.walkObservationSeconds;
            yield return new WaitForSecondsRealtime(t*.30f);child.Pose(Module4ChildPose.ExpectingPark);FaceChildTo(parkFocus);session.Play(Module4Event.childLooksAtPark);
            Caption("He looks towards the park. Leaving home like this has usually meant a park trip.","He seems to expect the park.");
            yield return new WaitForSecondsRealtime(t*.30f);session.Play(Module4Event.parkPassed);
            Caption("Today's destination is the mall. You do not turn in towards the park.","The expected plan has changed.");
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
            child.At(child.walkPosition);child.Pose(Module4ChildPose.Ready);
            ui.Show(replay?"Try the walk with a clear plan":"Walk with your child towards the park",
                "Follow the pavement to the marked area beside the park turn. Look at your child and the two possible directions.",
                "Walk into the marked area to continue. You can move freely.");ui.Busy();
            session.Play(Module4Event.walkApproachStarted);
            if(area){
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
            var b=new StringBuilder("<b>Clues you selected</b>\n");if(session.Data.clues.Count==0)b.AppendLine(ui.noCluesText);foreach(var c in session.Data.clues)b.AppendLine("• "+c);
            b.Append("\n<b>Your action</b>\n").AppendLine(session.Data.firstResponse).Append("\n<b>Child's response</b>\n").Append(session.Data.firstOutcome);return b.ToString();
        }
        void RenderGuesses(){
            pendingGuess=-1;bool review=CurrentStage==Module4Stage.ReviewGuesses;ui.Step(review?6:4,review?"Review your guesses":"Note your guesses");
            ui.Show(review?"Review your guesses":"What might be going on?",review?ReviewContext():"<b>Save at least 2 possible ideas.</b>\n\nChoose a guess, then how sure you are. You can keep more than one idea.",
                review?"Choose a guess to change your confidence.":"Saved ideas: "+session.Data.guesses.Count,true);
            bool[] selected=new bool[definition.guesses.Length];for(int i=0;i<selected.Length;i++)selected[i]=FindGuess(definition.guesses[i])!=null;
            Cards(definition.guesses,selected,guessPage,SelectGuess);ui.Tools(false,true);
            ui.footer.text="PAGE "+(guessPage+1)+" OF "+Pages(definition.guesses.Length)+"   |   "+session.Data.guesses.Count+" saved ideas";
            ui.Primary("Done",review?(UnityEngine.Events.UnityAction)CompleteGuessReview:CompleteGuesses,session.Data.guesses.Count>=2);
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
        public void CompleteGuesses(){if(CurrentStage!=Module4Stage.NoteGuesses||pendingGuess>=0||session.Data.guesses.Count<2)return;session.Play(Module4Event.guessesCompleted);ui.Step(5,"Look beyond the scene");session.Stage(Module4Stage.HomeFlashback);ui.Tools(false,false);ui.Show("Look beyond the scene","See what happened at home from your child's side. Think about what he could actually know.","Every time you leave like this, you usually go to the park.");ui.Primary("Look Beyond the Scene",LookBeyond);session.Play(Module4Event.lookBeyondOffered);}
        public void LookBeyond(){if(transitioning||CurrentStage!=Module4Stage.HomeFlashback||homeScene)return;session.Play(Module4Event.lookBeyondSelected);StartCoroutine(LoadVisit(Module4Stage.HomeFlashback,definition.homeScene));}
        IEnumerator HomeFlashback(){
            Stage(Module4Stage.HomeFlashback,5,"From your child's side");rig.SetMovementLocked(true);child.At(child.preparationPosition);
            if(perspectiveBodies)perspectiveBodies.EnterChildView(rig);else if(parentBody)parentBody.SetActive(true);
            if(definition.childEyeView){rig.EnterChildView(childViewPoint,definition.childEyeHeight);child.SetBodyVisible(false);}
            flashbackReady=false;ui.SetChildReturn(false);
            yield return session.Narrate(Module4Event.childViewEntered);yield return HomeTimeline(definition.flashbackSeconds,true);
            flashbackReady=true;ui.Show("What did your child expect?","You knew the plan had changed. Did he?\n\nHe saw the same getting-ready routine, and no one told him today's destination.","Think about what he has seen and what he could really know.");
            ui.Primary("Return to the Walk",ReturnToWalk);ui.SetChildReturn(true);session.Play(Module4Event.flashbackObservationFinished);session.Play(Module4Event.returnToWalkReady);
        }
        public void ReturnToWalk(){if(!homeScene||CurrentStage!=Module4Stage.HomeFlashback||!flashbackReady||transitioning)return;ui.SetChildReturn(false);session.Play(Module4Event.returnToWalkSelected);StartCoroutine(LoadVisit(Module4Stage.Respond,definition.walkScene));}
        public void BeginRespond(){Stage(Module4Stage.Respond,6,"Respond to your child");rig.SetMovementLocked(false);child.At(child.walkPosition);child.Pose(Module4ChildPose.Upset);Cry(true);ui.Show("How will you respond?","Your child is still upset. Choose a response and watch what happens.","Keep what he may be thinking in mind.",true);ui.Draw(definition.responses,null,ChooseResponse);session.Play(Module4Event.responseStepStarted);session.Play(Module4Event.responseChoicesShown);}
        public void ChooseResponse(int index){if(CurrentStage!=Module4Stage.Respond||responding||index<0||index>=definition.responses.Length)return;responding=true;StartCoroutine(Response(index));}
        IEnumerator Response(int index){
            ui.Show("Watch your child's response",definition.responses[index],"Interaction is paused while you watch.");ui.Busy();rig.SetMovementLocked(homeScene);
            session.Data.firstResponse=definition.responses[index];session.Data.firstResponseIndex=index;session.Log("ResponseSelected",definition.responses[index]);
            yield return session.Narrate(index==0?Module4Event.cardPictureBoard:index==1?Module4Event.cardNotToday:index==2?Module4Event.cardKeepWalking:Module4Event.cardRaiseVoice);
            if(index==0)child.ShowBoard(true);if(definition.responseDialoguePauseSeconds>0)yield return new WaitForSecondsRealtime(definition.responseDialoguePauseSeconds);
            yield return session.Narrate(Module4Event.responseChosen);
            yield return new WaitForSecondsRealtime(definition.responseObservationSeconds*.5f);
            bool calm=index<2;child.Pose(calm?Module4ChildPose.Settling:Module4ChildPose.MoreUpset);if(index==0)child.ShowBoard(true);if(calm)Cry(false);session.Play(Module4Event.responseMidpoint);
            Caption(calm?"The explanation is simple and calm. He starts to settle.":"He still does not understand the plan and appears more upset.","Keep watching his response.");
            yield return new WaitForSecondsRealtime(definition.responseObservationSeconds*.5f);while(session.IsNarrating)yield return null;
            child.Pose(calm?Module4ChildPose.Calmer:Module4ChildPose.MoreUpset);session.Data.firstOutcome=calm?"He began to settle after a calm explanation of the plan.":"He remained upset; today's plan was still unclear.";
            session.Data.firstResponseSeconds=definition.responseObservationSeconds;session.Play(Module4Event.responseObservationFinished);responding=false;rig.SetMovementLocked(false);
            Stage(Module4Stage.ReviewGuesses,6,"Review your guesses");session.Play(Module4Event.guessReviewStarted);RenderGuesses();
        }
        public void CompleteGuessReview(){if(CurrentStage!=Module4Stage.ReviewGuesses||pendingGuess>=0||session.Data.guesses.Count<2)return;session.Play(Module4Event.guessReviewCompleted);session.Stage(Module4Stage.Reflect);session.Data.reflectionQuestion=1;StartCoroutine(BeginReflection());}
        IEnumerator BeginReflection(){ui.Step(7,"Reflect");ui.Tools(false,false);yield return session.Narrate(Module4Event.reflectionStarted);yield return ReflectionQuestion();}
        IEnumerator ReflectionQuestion(){
            ui.Step(7,"Reflect");int q=Mathf.Clamp(session.Data.reflectionQuestion,1,3);ui.Reflection(definition.questions[q-1],q);
            yield return session.Narrate(q==1?Module4Event.reflectionQuestion1:q==2?Module4Event.reflectionQuestion2:Module4Event.reflectionQuestion3);
            ui.RefreshRecording();
        }
        public void ToggleAnswerRecording(){if(CurrentStage!=Module4Stage.Reflect||session.IsNarrating||answers.IsStarting)return;answers.Toggle(session.Data.reflectionQuestion);ui.RefreshRecording();}
        public void NextReflectionQuestion(){
            int q=session.Data.reflectionQuestion;if(CurrentStage!=Module4Stage.Reflect||session.IsNarrating||answers.IsRecording||answers.IsStarting||(definition.requireSavedAnswer&&!session.HasAnswer(q)))return;
            if(q<3){session.Data.reflectionQuestion++;session.Save();StartCoroutine(ReflectionQuestion());}else{session.Play(Module4Event.reflectionCompleted);session.Stage(Module4Stage.Feedback);ShowFeedback();}
        }
        public void ShowFeedback(){
            ui.Step(7,"Reflect");ui.Show("What your child could know",ReviewContext()+"\n\n<b>Thinking about his expectations</b>\n"+definition.feedback,"There are no right or wrong labels for your ideas.");
            ui.Primary("Replay and prevent",Replay);session.Play(Module4Event.feedbackShown);session.Play(Module4Event.replayOffered);
        }
        public void Replay(){if(CurrentStage!=Module4Stage.Feedback||transitioning||session.Data.replayUsed)return;session.Data.replayUsed=true;session.Play(Module4Event.replaySelected);StartCoroutine(LoadVisit(Module4Stage.ReplayHome,definition.homeScene));}
        void StartReplayHome(){
            Stage(Module4Stage.ReplayHome,8,"Replay and prevent");rig.SetMovementLocked(true);child.SetBodyVisible(true);child.At(child.preparationPosition);child.Pose(Module4ChildPose.Calm);session.Data.expectationsSet=false;
            ui.Show("Explain today's plan before leaving","This time, help your child know what you already know.\n\nToday's plan is the mall, then home.","Choose how you will set his expectations.",true);ui.Draw(definition.prevention,null,ChoosePrevention);session.Play(Module4Event.replayHomeStarted);
        }
        public void ChoosePrevention(int index){if(CurrentStage!=Module4Stage.ReplayHome||transitioning||responding||index<0||index>=definition.prevention.Length)return;responding=true;StartCoroutine(Prevention(index));}
        IEnumerator Prevention(int index){
            ui.Busy();yield return session.Narrate(index==0?Module4Event.replayPictureBoard:index==1?Module4Event.replayNotToday:index==2?Module4Event.replayFamiliarRoutine:Module4Event.replayExplainIfAsked);
            session.Data.replayResponse=definition.prevention[index];session.Data.expectationsSet=true;session.Data.replayPlanExplained=index<2;session.Log("ExpectationsSet",session.Data.replayResponse);child.Pose(Module4ChildPose.Ready);child.ShowBoard(index==0);
            ui.Show(session.Data.replayPlanExplained?"Today's plan is clear":"Try your chosen approach",session.Data.replayPlanExplained?"First the mall, then home.\n\nYour child has now been told what will happen before you leave.":"You are ready to leave. Notice how much of today's plan your child could understand from the response you chose.",session.Data.replayPlanExplained?"He is calm and knows the plan.":"Watch his response when you reach the park turn.");ui.Primary("Go to the Walk",GoToWalk);responding=false;session.Play(Module4Event.expectationsSet);
        }
        IEnumerator ReplayWalk(){
            Stage(Module4Stage.ReplayWalk,8,"Try the walk again");rig.SetMovementLocked(false);
            yield return WalkToCheckpoint(true);child.At(child.walkPosition);child.Pose(Module4ChildPose.Ready);Cry(false);
            ui.Show("The same walk, with a clear plan",session.Data.replayPlanExplained?"You take the same path. This time, your child knows that you are going to the mall.":"You take the same path. Notice whether your child understood today's destination before leaving.","You can move around to observe your child.");ui.Busy();
            yield return session.Narrate(Module4Event.replayWalkStarted);
            yield return new WaitForSecondsRealtime(definition.replayObservationSeconds*.5f);
            child.Pose(Module4ChildPose.ExpectingPark);session.Play(Module4Event.replayParkPassed);Caption(session.Data.replayPlanExplained?"You pass the park again. This time he has already heard the plan.":"You pass the park again. He looks towards the entrance and seems surprised when you continue.",session.Data.replayPlanExplained?"He knows that today is different.":"The park turn may still be unexpected for him.");
            yield return new WaitForSecondsRealtime(definition.replayObservationSeconds*.5f);while(session.IsNarrating)yield return null;
            child.Pose(session.Data.replayPlanExplained?Module4ChildPose.Calmer:Module4ChildPose.Upset);session.Data.replayOutcome=session.Data.replayPlanExplained?"He stayed calmer because the plan was explained before leaving.":"He was still surprised at the park turn; the destination had not been made clear.";
            session.Play(Module4Event.replayWalkFinished);session.Stage(Module4Stage.Summary);rig.SetMovementLocked(false);ShowSummary();
        }
        public void ShowSummary(){ui.Tools(false,false);ui.Summary(session.Data,definition);ui.Primary("Complete module",Complete);session.Play(Module4Event.summaryShown);}
        public void Complete(){if(CurrentStage!=Module4Stage.Summary||transitioning)return;StartCoroutine(FinishModule());}
        IEnumerator FinishModule(){if(video){ui.Busy();var saving=video.StopAndSave("Module completed");while(!saving.IsCompleted)yield return null;}session.Data.completed=true;session.Stage(Module4Stage.Complete);session.Play(Module4Event.moduleCompleted);session.Save();ShowCompleted();}
        void ShowCompleted(){ui.Step(8,"Complete");if(ui.progress)ui.progress.value=1;ui.Show("Module 4 completed","A familiar routine can create an expectation.\n\nExplaining a changed plan before leaving can help your child understand what will happen next.","Your session and saved answers are kept in the Module4 participant folder.");ui.Primary("Return to module selection",ReturnToEntry,Application.CanStreamedLevelBeLoaded("Autism_ModuleEntry"));}
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
            if(video)video.SuspendSceneCapture();
            var operation=SceneManager.LoadSceneAsync(sceneName,LoadSceneMode.Single);while(operation!=null&&!operation.isDone)yield return null;
        }
        public IEnumerator ResumeAfterHomePerspective(){
            transitioning=true;ui.SetChildReturn(false);fader.Black();rig.SetMovementLocked(true);
            yield return rig.RestoreWalkParentPose(session);
            if(video)video.BindScene(rig);rig.ConfigureUIInput();
            transitioning=false;yield return fader.Fade(false);BeginRespond();
            session.Log("ParentViewRestored",rig.head.position.ToString("F3"));
        }
        void OnDestroy(){gazeToken++;if(childSound)childSound.Stop();}
    }
}

