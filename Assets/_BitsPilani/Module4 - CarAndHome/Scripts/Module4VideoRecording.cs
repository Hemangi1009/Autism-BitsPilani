using System;
using System.Threading.Tasks;
using UnityEngine;
namespace BitsPilani.Module4 {
    // Lives with the Module 4 session, so one MP4 spans all Home/Walk visits.
    public sealed class Module4VideoRecording : MonoBehaviour {
        [Header("Module 4 video + game audio")]
        public bool recordVideo = true;
        public Vector2Int resolution = new Vector2Int(1280,720);
        [Range(10,60)] public int framesPerSecond = 30;
        [Tooltip("Same participant folder as the reflection WAV files. Windows-compatible time separators.")]
        public string timestampFormat = "yyyy-MM-dd_HH-mm-ss";
        public string LastError {get; private set;}
        public string LastSavedPath {get; private set;}
        public bool IsRecording => run && run.IsRecording;
        Module4RecordingRun run;
        Module4Rig rig;
        Task<bool> starting;
        bool finished;
        bool resumePending;
        void OnApplicationPause(bool paused){if(!paused&&!finished&&starting!=null)resumePending=true;}
        async void Update(){if(!resumePending||!rig||IsRecording)return;resumePending=false;await EnsureStarted();}
        public void SuspendSceneCapture(){if(run&&run.IsRecording)run.SuspendSceneCapture();}
        public void BindScene(Module4Rig sceneRig) {
            rig=sceneRig;
            if(run && run.IsRecording && rig && rig.head)
                run.BindSceneCamera(rig.head.GetComponent<Camera>(),rig.head.GetComponent<AudioListener>());
        }
        public Task<bool> EnsureStarted() {
            if(!recordVideo || IsRecording || finished)return Task.FromResult(true);
            return starting!=null&&!starting.IsCompleted?starting:(starting=StartCapture());
        }
        async Task<bool> StartCapture() {
            try {
                if(run){await run.StopAndSaveAsync("Resume after pause");Destroy(run.gameObject);run=null;}
                var session=GetComponent<Module4Session>();
                if(!session || string.IsNullOrEmpty(session.SessionFolder))throw new InvalidOperationException("Enter a valid participant ID first.");
                if(!rig || !rig.head)throw new InvalidOperationException("The Module 4 camera is not assigned.");
                var camera=rig.head.GetComponent<Camera>();var listener=rig.head.GetComponent<AudioListener>();
                if(!camera || !listener || !listener.enabled)throw new InvalidOperationException("Assign the tracked camera and its enabled AudioListener.");
                var host=new GameObject("Module4 video recording - survives scene transitions");
                run=host.AddComponent<Module4RecordingRun>();
                await run.StartCaptureAsync(camera,listener,session.SessionFolder,
                    "Video_"+DateTime.Now.ToString(timestampFormat).Replace(':','-'),
                    Mathf.Max(16,resolution.x/2*2),Mathf.Max(16,resolution.y/2*2),framesPerSecond);
                float deadline=Time.realtimeSinceStartup+10;
                while(run && run.IsRecording && run.LiveVideoFrames==0 && Time.realtimeSinceStartup<deadline)await Task.Yield();
                if(!run || !run.IsRecording || run.LiveVideoFrames==0)throw new InvalidOperationException("The camera produced no video frames. Check the native encoder console logs.");
                LastError=null;session.Log("VideoRecordingStarted",session.SessionFolder);return true;
            }catch(Exception ex){
                LastError=ex.Message;Debug.LogError("[M4][VideoStart] "+ex);
                if(run){await run.StopAndSaveAsync("Start failed");Destroy(run.gameObject);run=null;}
                return false;
            }
        }
        public async Task<string> StopAndSave(string reason) {
            finished=true;
            if(starting!=null&&!starting.IsCompleted)await starting;
            if(!run)return LastSavedPath;
            LastSavedPath=await run.StopAndSaveAsync(reason);LastError=run.Error;
            return LastSavedPath;
        }
        void OnDestroy(){if(run&&run.IsRecording)run.RequestStop("Left Module 4 early");}
    }
}
