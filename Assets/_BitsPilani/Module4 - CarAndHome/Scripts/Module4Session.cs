using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
namespace BitsPilani.Module4 {
    [Serializable] public sealed class Module4Guess { public string idea; public int confidence; }
    [Serializable] public sealed class Module4Answer { public int question; public string path; public float seconds; public string utc; public bool userSaved; }
    [Serializable] public sealed class Module4Log { public string utc, stage, action, detail; }
    [Serializable] public sealed class Module4RunData {
        public string participantId, runId, startedUtc;
        public Module4Stage stage = Module4Stage.HomeIntro;
        public List<string> clues = new List<string>();
        public List<Module4Guess> guesses = new List<Module4Guess>();
        public List<Module4Answer> answers = new List<Module4Answer>();
        public List<Module4Log> events = new List<Module4Log>();
        public List<string> skippedSteps = new List<string>();
        public string firstResponse = "No response selected", firstOutcome = "Not observed";
        public string replayResponse = "No plan selected", replayOutcome = "Not observed";
        public bool expectationsSet, replayUsed, completed;
        public bool replayPlanExplained;
        public int firstResponseIndex = -1;
        public float firstResponseSeconds = -1;
        public int reflectionQuestion = 1;
    }
    [DefaultExecutionOrder(-1000)]
    public sealed class Module4Session : MonoBehaviour {
        public static Module4Session Instance {get;private set;}
        public Module4Definition definition;
        public AudioSource voice;
        [Header("Module 4 answer and session storage")]
        public string rootFolderName = "Autism - Videos & Audios";
        [Tooltip("3-64 ASCII letters/numbers. Can also be supplied by the existing module entry session.")] public string participantId;
        public bool saveSessionToDisk = true;
        public Module4RunData Data {get;private set;}
        public string SessionFolder {get;private set;}
        [NonSerialized] public bool hasWalkParentPose;
        [NonSerialized] public Vector3 walkParentPosition;
        [NonSerialized] public Quaternion walkParentRotation;
        [NonSerialized] public Vector3 walkHeadPosition,walkCameraLocal,walkTrackingLocal;
        [NonSerialized] public Quaternion walkTrackingRotation;
        [NonSerialized] public float walkHeadYaw;
        Module4SceneController suspendedWalk;
        public BNG.VRUISystem SharedWalkUi {get;private set;}
        Transform sharedUiParent;
        UnityEngine.AsyncOperation preparedHome;
        ThreadPriority previousLoadingPriority;
        bool loadingPriorityChanged;
        readonly Dictionary<GameObject,bool> suspendedObjects=new Dictionary<GameObject,bool>();
        readonly Dictionary<Behaviour,bool> suspendedDrivers=new Dictionary<Behaviour,bool>();
        public bool HasSuspendedWalk => suspendedWalk;
        // Keep Skip locked until both scenes and their shared UI camera settle.
        public bool PerspectiveTransitioning {get;private set;}
        public bool IsNarrating => busy || queue.Count>0;
        bool busy;
        Coroutine voiceRoutine;VoiceJob activeVoiceJob;
        public bool DiscardingPreparedHome {get;private set;}
        public bool DiscardingPreparedHomeForExit {get;private set;}
        Module4SceneVoiceEvents sceneVoiceEvents;
        public void BindSceneVoiceEvents(Module4SceneVoiceEvents events){sceneVoiceEvents=events;}
        sealed class VoiceJob {public Module4Event id;public bool started,done;public Module4SceneVoiceEvents sceneEvents;public Module4VoiceCue cue;public string callbackKey;}
        readonly HashSet<string> completedVoiceEvents=new HashSet<string>();
        bool completingSkippedCallbacks;
        VoiceJob MakeVoiceJob(Module4Event id){
            var target=sceneVoiceEvents;
            int sceneKey=target?target.GetInstanceID():UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
            return new VoiceJob{id=id,sceneEvents=target,cue=definition?definition.Cue(id):null,callbackKey=sceneKey+":"+Data.stage+":"+id};
        }
        void StartVoiceCallbacks(VoiceJob job){
            if(job.started||job.done)return;job.started=true;
            job.cue?.onStarted?.Invoke();
            if(job.sceneEvents)job.sceneEvents.InvokeStarted(job.id);
        }
        void FinishVoiceCallbacks(VoiceJob job){
            if(job.done)return;
            StartVoiceCallbacks(job);if(job.done)return;
            // Mark first so callbacks cannot recursively complete this job twice.
            job.done=true;completedVoiceEvents.Add(job.callbackKey);
            job.cue?.onCompleted?.Invoke();
            if(job.sceneEvents)job.sceneEvents.InvokeCompleted(job.id);
        }
        readonly Queue<VoiceJob> queue = new Queue<VoiceJob>();
        void Awake() {
            if(Instance && Instance!=this){Destroy(gameObject);return;}
            Instance=this; DontDestroyOnLoad(gameObject);
            Data=new Module4RunData{runId=DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"),startedUtc=DateTime.UtcNow.ToString("o")};
            if(!voice)voice=GetComponent<AudioSource>();
            var folders=BitsPilani.Module1.Playground.FolderManager.Instance;
            if(folders && IsValidId(folders.ParticipantId))participantId=folders.ParticipantId;
            if(IsValidId(participantId))SetParticipant(participantId);
        }
        public static bool IsValidId(string id){return !string.IsNullOrEmpty(id)&&Regex.IsMatch(id,"\\A[A-Za-z0-9]{3,64}\\z");}
        public bool SetParticipant(string id) {
            if(!IsValidId(id))return false; participantId=id;Data.participantId=id;
            string root=Application.persistentDataPath;
#if !UNITY_ANDROID || UNITY_EDITOR
            string docs=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);if(!string.IsNullOrEmpty(docs))root=docs;
#endif
            SessionFolder=Path.Combine(root,rootFolderName,"Module4",id);
            try {if(saveSessionToDisk)Directory.CreateDirectory(SessionFolder);Log("ParticipantAccepted",id);return true;}
            catch(Exception ex){Debug.LogWarning("[M4][Storage] "+ex.Message);SessionFolder=Path.Combine(Application.persistentDataPath,"Module4",id);try{if(saveSessionToDisk)Directory.CreateDirectory(SessionFolder);return true;}catch(Exception inner){Debug.LogError("[M4][Storage] "+inner);return false;}}
        }
        public void Stage(Module4Stage stage){Data.stage=stage;Log("StageStarted",stage.ToString());}
        public void Log(string action,string detail=""){if(Data==null)return;Data.events.Add(new Module4Log{utc=DateTime.UtcNow.ToString("o"),stage=Data.stage.ToString(),action=action,detail=detail});Debug.Log("[M4]["+Data.stage+"] "+action+(string.IsNullOrEmpty(detail)?"":" | "+detail),this);Save();}
        public void Save(){if(!saveSessionToDisk||string.IsNullOrEmpty(SessionFolder))return;try{File.WriteAllText(Path.Combine(SessionFolder,"Session_"+Data.runId+".json"),JsonUtility.ToJson(Data,true));}catch(Exception ex){Debug.LogWarning("[M4][SessionSave] "+ex.Message);}}
        public void Play(Module4Event id){Enqueue(id);}
        VoiceJob Enqueue(Module4Event id){
            Log("Event",id.ToString());var job=MakeVoiceJob(id);
            if(completingSkippedCallbacks){FinishVoiceCallbacks(job);return job;}
            queue.Enqueue(job);if(!busy)voiceRoutine=StartCoroutine(Drain());return job;
        }
        public IEnumerator Narrate(Module4Event id){var job=Enqueue(id);while(!job.done)yield return null;}
        IEnumerator Drain(){
            busy=true;
            while(queue.Count>0){
                var job=queue.Dequeue();activeVoiceJob=job;var cue=job.cue;
                StartVoiceCallbacks(job);
                if(!job.done&&cue!=null&&cue.clip&&voice){
                    if(cue.delaySeconds>0)yield return new WaitForSecondsRealtime(cue.delaySeconds);
                    if(!job.done){voice.clip=cue.clip;voice.Play();while(voice&&voice.isPlaying&&!job.done)yield return null;}
                }
                FinishVoiceCallbacks(job);
            }
            busy=false;activeVoiceJob=null;voiceRoutine=null;
        }
        public void CancelNarration(bool completeCallbacks=false){
            if(voiceRoutine!=null)StopCoroutine(voiceRoutine);voiceRoutine=null;
            if(voice)voice.Stop();
            // Snapshot and clear before invoking user callbacks. Skip must not strand
            // an active event or play any queued VO while completing a step.
            var cancelled=new List<VoiceJob>();
            if(activeVoiceJob!=null)cancelled.Add(activeVoiceJob);
            activeVoiceJob=null;while(queue.Count>0)cancelled.Add(queue.Dequeue());busy=false;
            foreach(var job in cancelled){if(completeCallbacks)FinishVoiceCallbacks(job);else job.done=true;}
        }
        public void CompleteStepCallbacksOnSkip(Module4Stage step,bool homeScene){
            if(completingSkippedCallbacks)return;
            completingSkippedCallbacks=true;
            try{
                CancelNarration(true);
                // Lifecycle events only: never invent a selected card, a saved answer,
                // or a chosen response for a step the participant skipped.
                foreach(var id in SkippedStepLifecycle(step,homeScene)){
                    var job=MakeVoiceJob(id);
                    if(completedVoiceEvents.Contains(job.callbackKey))continue;
                    FinishVoiceCallbacks(job);Log("SkippedEventCompleted",id.ToString());
                }
            }finally{completingSkippedCallbacks=false;}
        }
        static Module4Event[] SkippedStepLifecycle(Module4Stage step,bool homeScene){
            switch(step){
                case Module4Stage.HomeIntro:return new[]{Module4Event.homeStarted,Module4Event.childGazeStarted,Module4Event.childGazeCompleted,Module4Event.homeObservationStarted,Module4Event.shoesPutOn,Module4Event.usualThingsPickedUp,Module4Event.parentSaysLetsGo,Module4Event.homeObservationFinished};
                case Module4Stage.WalkObserve:return new[]{Module4Event.walkApproachStarted,Module4Event.walkCheckpointReached,Module4Event.walkStarted,Module4Event.walkObservationStarted,Module4Event.childLooksAtPark,Module4Event.parkPassed,Module4Event.childUpset,Module4Event.walkObservationFinished};
                case Module4Stage.CollectClues:return new[]{Module4Event.clueStepStarted,Module4Event.clueToolOpened,Module4Event.cluesCompleted};
                case Module4Stage.NoteGuesses:return new[]{Module4Event.guessStepStarted,Module4Event.guessToolOpened,Module4Event.guessesCompleted};
                case Module4Stage.HomeFlashback:return homeScene
                    ?new[]{Module4Event.childViewEntered,Module4Event.homeObservationStarted,Module4Event.shoesPutOn,Module4Event.usualThingsPickedUp,Module4Event.parentSaysLetsGo,Module4Event.flashbackObservationFinished,Module4Event.returnToWalkReady}
                    :new[]{Module4Event.lookBeyondOffered,Module4Event.flashbackObservationFinished};
                case Module4Stage.Respond:return new[]{Module4Event.responseStepStarted,Module4Event.responseChoicesShown,Module4Event.responseObservationFinished,Module4Event.guessReviewStarted,Module4Event.guessToolOpened,Module4Event.guessReviewCompleted};
                case Module4Stage.ReviewGuesses:return new[]{Module4Event.guessReviewStarted,Module4Event.guessToolOpened,Module4Event.guessReviewCompleted};
                case Module4Stage.Reflect:return new[]{Module4Event.reflectionStarted,Module4Event.reflectionCompleted};
                case Module4Stage.Feedback:return new[]{Module4Event.feedbackShown,Module4Event.replayOffered};
                case Module4Stage.ReplayHome:return new[]{Module4Event.replayHomeStarted,Module4Event.replayWalkFinished};
                case Module4Stage.ReplayWalk:return new[]{Module4Event.walkApproachStarted,Module4Event.walkCheckpointReached,Module4Event.replayWalkStarted,Module4Event.replayParkPassed,Module4Event.replayWalkFinished};
                default:return Array.Empty<Module4Event>();
            }
        }
        // A prepared additive load must be released before another scene transition.
        // Disable its authored roots before Start, then unload it without showing UI.
        public IEnumerator DiscardPreparedPerspective(bool exitingModule=false){
            if(preparedHome==null)yield break;
            DiscardingPreparedHomeForExit=exitingModule;DiscardingPreparedHome=true;RestoreLoadingPriority();
            var operation=preparedHome;operation.allowSceneActivation=true;
            while(!operation.isDone)yield return null;
            // Exit-only: allow unused package rig components to finish Start before OnDisable.
            // The sceneLoaded handler hides visuals/audio/input, so the scene is never presented.
            if(exitingModule)yield return null;
            var preparedScene=UnityEngine.SceneManagement.SceneManager.GetSceneByName(definition.homeScene);
            if(preparedScene.IsValid()&&preparedScene.isLoaded){var unloading=UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(preparedScene);if(unloading!=null)while(!unloading.isDone)yield return null;}
            preparedHome=null;DiscardingPreparedHome=false;DiscardingPreparedHomeForExit=false;Log("PreparedPerspectiveDiscarded");
        }
        public bool HasAnswer(int question){return Data.answers.Exists(a=>a.question==question&&a.userSaved);}
        public void FreshRun(){
            completedVoiceEvents.Clear();
            var id=participantId;
            Data=new Module4RunData{participantId=id,runId=DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"),startedUtc=DateTime.UtcNow.ToString("o")};
            Log("NewRun");
        }
        // A child-perspective visit is an overlay, not a replacement of the Walk.
        // Keep the original rig and environment resident, with only its InputBridge
        // active, so the simulator/XR input and the parent's pose survive the visit.
        public void PrepareHomePerspective(){
            if(preparedHome!=null||HasSuspendedWalk)return;
            preparedHome=UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(definition.homeScene,UnityEngine.SceneManagement.LoadSceneMode.Additive);
            preparedHome.allowSceneActivation=false;
            previousLoadingPriority=Application.backgroundLoadingPriority;loadingPriorityChanged=true;
            Application.backgroundLoadingPriority=ThreadPriority.Low;
            StartCoroutine(WaitForPreparedHome());Log("HomePerspectivePreloadStarted");
        }
        IEnumerator WaitForPreparedHome(){
            while(preparedHome!=null&&preparedHome.progress<.9f)yield return null;
            RestoreLoadingPriority();Log("HomePerspectivePreloadReady");
        }
        void RestoreLoadingPriority(){if(loadingPriorityChanged){Application.backgroundLoadingPriority=previousLoadingPriority;loadingPriorityChanged=false;}}
        public IEnumerator OpenHomePerspective(Module4SceneController walk){
            PerspectiveTransitioning=true;float start=Time.realtimeSinceStartup;suspendedWalk=walk;
            SharedWalkUi=walk.rig.vrUi;
            if(SharedWalkUi){sharedUiParent=SharedWalkUi.transform.parent;SharedWalkUi.transform.SetParent(transform,false);}
            var recording=GetComponent<Module4VideoRecording>();if(recording)recording.SuspendSceneCapture();
            var loading=preparedHome??UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(definition.homeScene,UnityEngine.SceneManagement.LoadSceneMode.Additive);
            loading.allowSceneActivation=false;
            while(loading.progress<.9f)yield return null;
            RestoreLoadingPriority();
            suspendedObjects.Clear();suspendedDrivers.Clear();
            var rigRoot=walk.rig.transform.root.gameObject;
            foreach(var root in walk.gameObject.scene.GetRootGameObjects()){
                if(root==rigRoot)continue;RememberAndHide(root);
            }
            foreach(Transform child in rigRoot.transform)RememberAndHide(child.gameObject);
            foreach(var behaviour in rigRoot.GetComponents<Behaviour>()){
                if(behaviour is BNG.InputBridge)continue;
                suspendedDrivers[behaviour]=behaviour.enabled;behaviour.enabled=false;
            }
            loading.allowSceneActivation=true;while(!loading.isDone)yield return null;
            preparedHome=null;
            var home=UnityEngine.SceneManagement.SceneManager.GetSceneByName(definition.homeScene);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(home);
            PerspectiveTransitioning=false;
            Log("HomePerspectiveLoaded","Walk retained; seconds="+(Time.realtimeSinceStartup-start).ToString("F2"));
        }
        void RememberAndHide(GameObject obj){suspendedObjects[obj]=obj.activeSelf;obj.SetActive(false);}
        public IEnumerator ResumeWalkPerspective(Module4SceneController home){
            PerspectiveTransitioning=true;float start=Time.realtimeSinceStartup;
            var recording=GetComponent<Module4VideoRecording>();if(recording)recording.SuspendSceneCapture();
            var homeScene=home.gameObject.scene;
            foreach(var root in homeScene.GetRootGameObjects())root.SetActive(false);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(suspendedWalk.gameObject.scene);
            foreach(var entry in suspendedObjects)if(entry.Key)entry.Key.SetActive(entry.Value);
            foreach(var entry in suspendedDrivers)if(entry.Key)entry.Key.enabled=entry.Value;
            suspendedObjects.Clear();suspendedDrivers.Clear();
            if(SharedWalkUi&&sharedUiParent)SharedWalkUi.transform.SetParent(sharedUiParent,false);
            // No LoadScene call for Walk: resume the same objects and the same rig.
            yield return suspendedWalk.ResumeAfterHomePerspective();
            var unloading=UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(homeScene);
            if(unloading!=null)while(!unloading.isDone)yield return null;
            // Home teardown can replace canvas event cameras. Rebind before unlocking Skip.
            suspendedWalk.rig.ConfigureUIInput();
            suspendedWalk=null;
            SharedWalkUi=null;sharedUiParent=null;PerspectiveTransitioning=false;
            Log("WalkResumedWithoutReload","seconds="+(Time.realtimeSinceStartup-start).ToString("F2"));
        }
        void OnApplicationPause(bool pause){if(pause)Save();}
        void OnApplicationQuit(){if(preparedHome!=null&&!preparedHome.isDone)preparedHome.allowSceneActivation=true;Save();}
        void OnEnable(){UnityEngine.SceneManagement.SceneManager.sceneLoaded+=SceneLoaded;}
        void OnDisable(){UnityEngine.SceneManagement.SceneManager.sceneLoaded-=SceneLoaded;}
        void SceneLoaded(UnityEngine.SceneManagement.Scene scene,UnityEngine.SceneManagement.LoadSceneMode mode){
            if(Instance==this&&DiscardingPreparedHome&&definition&&scene.name==definition.homeScene){
                foreach(var root in scene.GetRootGameObjects()){
                    if(!DiscardingPreparedHomeForExit){root.SetActive(false);continue;}
                    // Keep the unused rig active for one initialization frame without rendering
                    // or accepting input; its simulator otherwise throws when disabled before Start.
                    foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
                    foreach(var canvas in root.GetComponentsInChildren<Canvas>(true))canvas.enabled=false;
                    foreach(var camera in root.GetComponentsInChildren<Camera>(true))camera.enabled=false;
                    foreach(var listener in root.GetComponentsInChildren<AudioListener>(true))listener.enabled=false;
                    foreach(var audio in root.GetComponentsInChildren<AudioSource>(true))audio.Stop();
                    foreach(var input in root.GetComponentsInChildren<UnityEngine.EventSystems.BaseInputModule>(true))input.enabled=false;
                    foreach(var events in root.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true))events.enabled=false;
                    foreach(var pointer in root.GetComponentsInChildren<BNG.UIPointer>(true))pointer.enabled=false;
                }
                return;
            }
            if(Instance==this && definition && scene.name!=definition.homeScene && scene.name!=definition.walkScene){Save();Destroy(gameObject);}
        }
        void OnDestroy(){if(Instance==this){RestoreLoadingPriority();if(preparedHome!=null&&!preparedHome.isDone)preparedHome.allowSceneActivation=true;Instance=null;}}
    }
}

