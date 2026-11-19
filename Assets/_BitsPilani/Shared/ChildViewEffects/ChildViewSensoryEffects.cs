using System.Collections.Generic;
using UnityEngine;
using BitsPilani.Module1.Playground;
using BitsPilani.Module4;
namespace BitsPilani.Shared {
    [DisallowMultipleComponent]
    [AddComponentMenu("BitsPilani/Child View Sensory Effects")]
    public sealed class ChildViewSensoryEffects : MonoBehaviour {
        [Header("Child-view references - configured in the scene")]
        public PlaygroundFlowController module1;
        public Module4SceneController module4;
        public Camera viewCamera;
        [Tooltip("Apply the same effect to this module's video recorder camera.")] public bool includeInRecordings=true;
        [Header("Brightness and blur - all appearance comes from these Inspector values")]
        public bool enableFlashes=true;
        [Range(0,1)] public float intensity=1;
        [Range(0,4),Tooltip("Peak brightness increase in exposure stops. Zero keeps exposure unchanged.")] public float peakExposureEV=1.25f;
        [Range(0,1),Tooltip("Peak wash towards Flash Color. Zero disables the wash.")] public float peakWhiteWash=.12f;
        public Color flashColor=Color.white;
        [Range(0,24),Tooltip("Peak blur radius in rendered pixels, scaled by pulse strength. Zero disables blur.")] public float peakBlurPixels=3;
        [Header("Pulse timing (seconds)")]
        [Min(0)] public float startDelay=1.5f;
        [Min(.01f)] public float fadeInSeconds=.3f;
        [Min(0)] public float holdSeconds=.1f;
        [Min(.01f)] public float fadeOutSeconds=.7f;
        [Header("Random pause between separate effects (seconds)")]
        [Min(0), Tooltip("Shortest pause between randomly chosen effects.")]
        public float minimumGapSeconds=1f;
        [UnityEngine.Serialization.FormerlySerializedAs("gapSeconds")]
        [Min(0), Tooltip("Longest pause. A new random value between Minimum and Maximum is chosen for each pause.")]
        public float maximumGapSeconds=3f;
        [Tooltip("Repeat randomly chosen effects. Consecutive matching choices merge into one smooth effect. If off, play two choices, merged when they match.")] public bool repeat=true;
        [Range(1,6), Tooltip("Maximum matching random choices merged into one continuous effect. Each extra choice extends the hold instead of fading out and restarting.")]
        public int maximumMergedPulses=3;
        [Tooltip("Curve from 0 to 1 for brightening.")] public AnimationCurve riseCurve=AnimationCurve.EaseInOut(0,0,1,1);
        [Tooltip("Curve from 0 to 1 for recovery; the result is inverted.")] public AnimationCurve fallCurve=AnimationCurve.EaseInOut(0,0,1,1);
        [Header("Optional Look Beyond audio - no generated VO")]
        [Tooltip("Optional child-view audio. Empty uses the assigned AudioSource clip, if any.")] public AudioClip childViewAudio;
        [Tooltip("Dedicated scene AudioSource. Edit its volume, pitch and Loop in the Inspector.")] public AudioSource audioSource;
        [Header("Play-mode preview (optional)")]
        [Tooltip("Preview without entering a module step. Turn off to restore the normal camera.")] public bool previewInPlayMode;
        [Tooltip("Hold a fixed strength for tuning instead of cycling.")] public bool holdPreviewStrength;
        public enum EffectKind { WhiteFlash, Blur }
        [Tooltip("Which separate effect to show when holding a fixed preview strength.")] public EffectKind previewEffect=EffectKind.WhiteFlash;
        [Range(0,1)] public float previewStrength=1;
        public bool IsRunning {get;private set;}
        public float CurrentPulse => !IsRunning||!enableFlashes?0:
            previewInPlayMode&&holdPreviewStrength?Mathf.Clamp01(previewStrength)*intensity:
            sequenceFinished?0:EvaluateEnvelope(Time.unscaledTime-pulseStartsAt)*intensity;
        // Supply mutually exclusive brightness/white-wash and blur channels to both renderer paths.
        public Vector4 CurrentShaderPulse {
            get {
                float pulse=CurrentPulse;
                bool blur=previewInPlayMode&&holdPreviewStrength?previewEffect==EffectKind.Blur:blurPulse;
                return blur?new Vector4(0,0,peakBlurPixels*pulse,0):
                    new Vector4(peakExposureEV*pulse,peakWhiteWash*pulse,0,0);
            }
        }
        float pulseStartsAt;
        bool blurPulse,sequenceFinished;
        int completedPulses,pulsesInBurst;
        bool nextBlurPulse;
        float extraHoldSeconds;
        AudioClip previousClip;bool previousPlaying,audioOwned;
        static readonly List<ChildViewSensoryEffects> active=new List<ChildViewSensoryEffects>();
        public bool WantsChildView => Application.isPlaying&&(previewInPlayMode||
            (module1&&module1.isActiveAndEnabled&&module1.rig&&module1.rig.InChildView&&module1.CurrentStep==PlaygroundStep.ChildPerspective)||
            (module4&&module4.isActiveAndEnabled&&module4.homeScene&&!module4.IsTransitioning&&module4.rig&&module4.rig.InChildView&&module4.CurrentStage==Module4Stage.HomeFlashback));
        // Evaluate a single pulse. The sequence clock selects the effect and random pause separately.
        public float PulseAt(float elapsed){return EvaluateEnvelope(elapsed-Mathf.Max(0,startDelay));}
        float BasePulseDuration => Mathf.Max(.01f,fadeInSeconds)+Mathf.Max(0,holdSeconds)+Mathf.Max(.01f,fadeOutSeconds);
        float PulseDuration => BasePulseDuration+extraHoldSeconds;
        void PlanPulseRun(){
            pulsesInBurst=1;
            nextBlurPulse=!blurPulse;
            int limit=Mathf.Clamp(maximumMergedPulses,1,6);
            if(!repeat)limit=Mathf.Min(limit,Mathf.Max(1,2-completedPulses));
            // Look ahead before fading in. Matching random choices become a longer hold,
            // with one fade-in and one fade-out rather than a visible cut/restart.
            while(pulsesInBurst<limit){
                bool choice=Random.value<.5f;
                if(choice!=blurPulse){nextBlurPulse=choice;break;}
                pulsesInBurst++;
            }
            extraHoldSeconds=(pulsesInBurst-1)*BasePulseDuration;
        }
        float EvaluateEnvelope(float t){
            float up=Mathf.Max(.01f,fadeInSeconds),hold=Mathf.Max(0,holdSeconds)+extraHoldSeconds,down=Mathf.Max(.01f,fadeOutSeconds);
            if(t<0||t>=up+hold+down)return 0;
            if(t<up)return Mathf.Clamp01(riseCurve==null?t/up:riseCurve.Evaluate(t/up));
            if(t<up+hold)return 1;
            float recovery=(t-up-hold)/down;return 1-Mathf.Clamp01(fallCurve==null?recovery:fallCurve.Evaluate(recovery));
        }
        void AdvanceSequence(){
            if(sequenceFinished||(previewInPlayMode&&holdPreviewStrength)||Time.unscaledTime<pulseStartsAt+PulseDuration)return;
            completedPulses+=pulsesInBurst;
            if(!repeat&&completedPulses>=2){sequenceFinished=true;return;}
            // Sample once per pause, never once per frame or per camera. Effects cannot overlap.
            float minimum=Mathf.Max(0,Mathf.Min(minimumGapSeconds,maximumGapSeconds));
            float maximum=Mathf.Max(minimum,Mathf.Max(minimumGapSeconds,maximumGapSeconds));
            float gap=Random.Range(minimum,maximum);
            // The next distinct random choice starts only after the current effect fully fades out.
            blurPulse=nextBlurPulse;
            PlanPulseRun();
            pulseStartsAt=Time.unscaledTime+gap;
        }
        void OnEnable(){if(!active.Contains(this))active.Add(this);}
        void LateUpdate(){if(WantsChildView&&!IsRunning)Begin();else if(!WantsChildView&&IsRunning)StopEffect();if(IsRunning)AdvanceSequence();}
        void Begin(){
            IsRunning=true;blurPulse=Random.value<.5f;completedPulses=0;sequenceFinished=false;PlanPulseRun();pulseStartsAt=Time.unscaledTime+Mathf.Max(0,startDelay);
            if(audioSource&&(childViewAudio||audioSource.clip)){
                previousClip=audioSource.clip;previousPlaying=audioSource.isPlaying;audioOwned=true;
                if(childViewAudio)audioSource.clip=childViewAudio;audioSource.Play();
            }
            Debug.Log("[ChildViewEffects] Started randomly selected white flash/blur pulses with random gaps and optional audio: "+gameObject.scene.name,this);
        }
        public void StopEffect(){
            if(audioOwned&&audioSource){audioSource.Stop();audioSource.clip=previousClip;if(previousPlaying&&previousClip)audioSource.Play();}
            audioOwned=false;bool wasRunning=IsRunning;IsRunning=false;
            if(wasRunning)Debug.Log("[ChildViewEffects] Stopped; normal rendering/audio restored: "+gameObject.scene.name,this);
        }
        void OnDisable(){StopEffect();active.Remove(this);}
        void OnDestroy(){StopEffect();active.Remove(this);}
        public static ChildViewSensoryEffects ForCamera(Camera camera){
            if(!camera||camera.cameraType!=CameraType.Game)return null;
            for(int i=active.Count-1;i>=0;i--){var fx=active[i];if(!fx||!fx.isActiveAndEnabled||!fx.IsRunning||!fx.WantsChildView)continue;
                if(camera==fx.viewCamera)return fx;
                if(fx.includeInRecordings&&((fx.module1&&camera.GetComponent<PlaygroundCameraCapture>())||(fx.module4&&camera.GetComponent<Module4CameraCapture>())))return fx;
            }return null;
        }
    }
}
