using UnityEngine;
namespace BitsPilani.Module4 {
    public sealed class Module4Music : MonoBehaviour {
        [Header("Soft background music - editable session source")]
        public AudioSource source;
        [Range(0,1)] public float normalVolume=.16f;
        [Range(0,1),Tooltip("Reduced volume while a VO or microphone answer is active.")]
        public float narrationVolume=.035f;
        void Start(){if(source&&source.clip){source.loop=true;source.Play();}}
        void Update(){if(!source)return;var s=GetComponent<Module4Session>();var a=GetComponent<Module4AnswerRecorder>();bool quiet=s&&s.IsNarrating||a&&a.IsRecording;source.volume=Mathf.MoveTowards(source.volume,quiet?narrationVolume:normalVolume,Time.unscaledDeltaTime*.2f);}
    }
}
