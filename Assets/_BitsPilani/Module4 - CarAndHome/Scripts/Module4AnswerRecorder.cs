using System;using System.IO;using System.Collections;using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif
using BitsPilani.Module1.Playground;
namespace BitsPilani.Module4 {
    // Module 4 adapter; the unchanged PCM/prompt helpers are fully reusable from Module 1.
    public sealed class Module4AnswerRecorder : MonoBehaviour {
        public Module4Session session;
        [Header("Microphone WAV answers")]
        public string microphoneDevice;
        [Range(8000,48000)] public int sampleRate=24000;
        public string filePrefix="Audio";
        public bool prependQuestionVoiceover=true;
        public bool IsRecording {get;private set;}
        public bool IsStarting {get;private set;}
        public string Status {get;private set;} = "Record at least one answer, then select Next.";
        public string LastSavedPath {get;private set;}
        AudioClip microphone;PlaygroundPcmWavWriter writer;
        int readPosition,question;long answerFrames;string currentPath;float[] buffer;float lastCheckpoint;
        string Device => string.IsNullOrEmpty(microphoneDevice)?null:microphoneDevice;
        public void RequestPermission(){
#if UNITY_ANDROID && !UNITY_EDITOR
            if(!Permission.HasUserAuthorizedPermission(Permission.Microphone))Permission.RequestUserPermission(Permission.Microphone);
#endif
        }
        public void Toggle(int questionNumber){
            if(IsStarting)return;
            if(IsRecording){StopAndSave();return;}
            if(!session||session.IsNarrating){Status="Please wait until the question voiceover ends.";return;}
            StartCoroutine(Begin(questionNumber));
        }
        IEnumerator Begin(int questionNumber){
            IsStarting=true;Status="Starting microphone...";
            yield return session.Narrate(Module4Event.answerRecordingStarted);
#if UNITY_ANDROID && !UNITY_EDITOR
            if(!Permission.HasUserAuthorizedPermission(Permission.Microphone)){
                bool finished=false,granted=false;var cb=new PermissionCallbacks();
                cb.PermissionGranted+=_=>{granted=true;finished=true;};
                cb.PermissionDenied+=_=>finished=true;
                cb.PermissionDeniedAndDontAskAgain+=_=>finished=true;
                Permission.RequestUserPermission(Permission.Microphone,cb);
                float until=Time.realtimeSinceStartup+30;while(!finished&&Time.realtimeSinceStartup<until)yield return null;
                if(!granted&&!Permission.HasUserAuthorizedPermission(Permission.Microphone)){Status="Microphone permission was not granted. Enable it in app permissions, then try Record again.";IsStarting=false;yield break;}
            }
#endif
            if(Microphone.devices.Length==0){Status="No microphone found. Connect or enable a microphone, then try again.";IsStarting=false;yield break;}
            try{
                if(string.IsNullOrEmpty(session.SessionFolder)||!session.saveSessionToDisk)throw new IOException("Enter a valid participant ID before recording.");
                Directory.CreateDirectory(session.SessionFolder);question=questionNumber;
                int take=session.Data.answers.FindAll(a=>a.question==question).Count+1;
                do{currentPath=Path.Combine(session.SessionFolder,filePrefix+"_"+DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")+"_ans"+question+"_take"+take.ToString("00")+".wav");take++;}while(File.Exists(currentPath));
                microphone=Microphone.Start(Device,true,10,sampleRate);
                if(!microphone)throw new IOException("The microphone could not start.");
            }catch(Exception ex){Status=ex.Message;IsStarting=false;Debug.LogWarning("[M4][Audio] "+ex.Message,this);yield break;}
            float limit=Time.realtimeSinceStartup+5;
            while(Microphone.GetPosition(Device)<=0&&Time.realtimeSinceStartup<limit)yield return null;
            if(Microphone.GetPosition(Device)<=0){Microphone.End(Device);Status="The microphone did not produce audio. Try Record again.";IsStarting=false;yield break;}
            try{
                writer=new PlaygroundPcmWavWriter(currentPath,microphone.frequency,microphone.channels);
                var prompt=session.definition.QuestionClip(question);
                if(prependQuestionVoiceover&&prompt)try{PlaygroundQuestionAudio.AppendPrompt(writer,prompt,microphone.frequency,microphone.channels);}catch(Exception ex){Debug.LogWarning("[M4][Audio] Question VO could not be prepended. Use Decompress On Load for the assigned clip. "+ex.Message);}
                readPosition=0;answerFrames=0;buffer=new float[4096*microphone.channels];
                IsRecording=true;IsStarting=false;Status="Recording answer "+question+". Select Save recording when finished.";lastCheckpoint=Time.realtimeSinceStartup;
            }catch(Exception ex){Microphone.End(Device);writer?.Dispose();writer=null;Status="Recording could not start: "+ex.Message;IsStarting=false;}
        }
        void Update(){if(IsRecording)Poll();}
        void Poll(){
            if(!microphone||writer==null)return;
            int position=Microphone.GetPosition(Device);if(position<0)return;
            int available=position-readPosition;if(available<0)available+=microphone.samples;
            while(available>0){
                int count=Mathf.Min(4096,available);
                if(!microphone.GetData(buffer,readPosition)){Status="Could not read the microphone.";break;}
                writer.Append(buffer,count*microphone.channels);answerFrames+=count;
                readPosition=(readPosition+count)%microphone.samples;available-=count;
            }
            if(Time.realtimeSinceStartup-lastCheckpoint>=1){writer.Checkpoint();lastCheckpoint=Time.realtimeSinceStartup;}
        }
        public bool StopAndSave(bool userSaved=true){
            if(!IsRecording)return false;
            try{
                Poll();Microphone.End(Device);IsRecording=false;writer?.Dispose();writer=null;
                if(answerFrames<sampleRate*.15f){if(File.Exists(currentPath))File.Delete(currentPath);Status="That recording was too short. Please record your answer again.";return false;}
                LastSavedPath=currentPath;
                session.Data.answers.Add(new Module4Answer{question=question,path=currentPath,seconds=(float)answerFrames/microphone.frequency,utc=DateTime.UtcNow.ToString("o"),userSaved=userSaved});
                Status=userSaved?"Answer saved. Record another clip or select Next.":"Clip auto-saved. Record and select Save recording to continue.";
                session.Log("AnswerSaved","Question "+question+" | "+currentPath);session.Play(Module4Event.answerSaved);
                return true;
            }catch(Exception ex){IsRecording=false;try{Microphone.End(Device);writer?.Dispose();}catch{}writer=null;Status="Could not save answer: "+ex.Message;Debug.LogError("[M4][Audio] "+ex);return false;}
        }
        void OnApplicationPause(bool pause){if(pause&&IsRecording)StopAndSave(false);}
        void OnApplicationQuit(){if(IsRecording)StopAndSave(false);}
        void OnDestroy(){if(IsRecording)StopAndSave();}
    }
}

