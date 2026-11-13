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
        public bool IsNarrating => busy || queue.Count>0;
        bool busy;
        sealed class VoiceJob {public Module4Event id;public bool done;}
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
            Log("Event",id.ToString());var job=new VoiceJob{id=id};queue.Enqueue(job);if(!busy)StartCoroutine(Drain());return job;
        }
        public IEnumerator Narrate(Module4Event id){var job=Enqueue(id);while(!job.done)yield return null;}
        IEnumerator Drain(){
            busy=true;
            while(queue.Count>0){var job=queue.Dequeue();var cue=definition?definition.Cue(job.id):null;
                cue?.onStarted?.Invoke();
                if(cue!=null&&cue.clip&&voice){if(cue.delaySeconds>0)yield return new WaitForSecondsRealtime(cue.delaySeconds);voice.clip=cue.clip;voice.Play();while(voice&&voice.isPlaying)yield return null;}
                cue?.onCompleted?.Invoke();job.done=true;
            }
            busy=false;
        }
        public bool HasAnswer(int question){return Data.answers.Exists(a=>a.question==question&&a.userSaved);}
        public void FreshRun(){
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
            float start=Time.realtimeSinceStartup;suspendedWalk=walk;
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
            Log("HomePerspectiveLoaded","Walk retained; seconds="+(Time.realtimeSinceStartup-start).ToString("F2"));
        }
        void RememberAndHide(GameObject obj){suspendedObjects[obj]=obj.activeSelf;obj.SetActive(false);}
        public IEnumerator ResumeWalkPerspective(Module4SceneController home){
            float start=Time.realtimeSinceStartup;
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
            suspendedWalk=null;
            SharedWalkUi=null;sharedUiParent=null;
            Log("WalkResumedWithoutReload","seconds="+(Time.realtimeSinceStartup-start).ToString("F2"));
        }
        void OnApplicationPause(bool pause){if(pause)Save();}
        void OnApplicationQuit(){Save();}
        void OnEnable(){UnityEngine.SceneManagement.SceneManager.sceneLoaded+=SceneLoaded;}
        void OnDisable(){UnityEngine.SceneManagement.SceneManager.sceneLoaded-=SceneLoaded;}
        void SceneLoaded(UnityEngine.SceneManagement.Scene scene,UnityEngine.SceneManagement.LoadSceneMode mode){
            if(Instance==this && definition && scene.name!=definition.homeScene && scene.name!=definition.walkScene){Save();Destroy(gameObject);}
        }
        void OnDestroy(){if(Instance==this){RestoreLoadingPriority();if(preparedHome!=null&&!preparedHome.isDone)preparedHome.allowSceneActivation=true;Instance=null;}}
    }
}

