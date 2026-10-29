using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


namespace BitsPilani.Training {
[AddComponentMenu("BitsPilani/Original Module 1 Steps 1 and 2 Flow")]
public class OriginalModule12Flow : MonoBehaviour {
    public enum Phase { EnterPlayground, WatchChanges, ObservationHold }
    public Phase CurrentPhase { get; private set; }
    [Header("Scene references")]
    public Transform child, partyTarget, onlooker, head;
    public GameObject guidedMarker, burstBalloon;
    public Transform[] balloonStrings;
    public Transform[] upperArms, forearms, hands;
    public Renderer[] eyes;
    public AudioSource partyAudio, popAudio, narration;
    public AudioClip entryVoice, changeVoice;
    public Text title, instructions, status, wristTitle, wristStatus, actionLabel, wristActionLabel;
    public Button actionButton, restartButton, wristActionButton;
    public Slider progress;
    public Canvas wristCanvas;
    public Transform leftController;
    public Behaviour[] lockDuringEvent;
    public float entryLookSeconds = 8f;
    [Range(20,30)] public float eventSeconds = 25f;
    public bool guidedPractice = true;
    readonly Dictionary<Behaviour,bool> previousEnabled = new Dictionary<Behaviour,bool>();
    Vector3 childPosition; Quaternion childRotation;
    Vector3[] armPos, forePos, handPos; Quaternion[] armRot,foreRot,handRot;
    Vector3[] eyePos, armScale, foreScale;
    float enteredAt;
    float[] dwell = new float[3];
    bool[] noticed = new bool[3];
    Coroutine sequence;
    AudioClip musicClip, popClip;
    public bool ReadyForStepTwo => CurrentPhase == Phase.EnterPlayground && Time.time-enteredAt >= entryLookSeconds;
    void Start() {
        childPosition=child.localPosition; childRotation=child.localRotation;
        armScale=Scales(upperArms);foreScale=Scales(forearms);armPos=Positions(upperArms);armRot=Rotations(upperArms);forePos=Positions(forearms);foreRot=Rotations(forearms);handPos=Positions(hands);handRot=Rotations(hands);
        eyePos=new Vector3[eyes.Length];for(int i=0;i<eyes.Length;i++)eyePos[i]=eyes[i].transform.localPosition;
        actionButton.onClick.AddListener(PrimaryAction);wristActionButton.onClick.AddListener(PrimaryAction);restartButton.onClick.AddListener(RestartModule);
        musicClip=BuildMusic();popClip=BuildPop();partyAudio.clip=musicClip;partyAudio.loop=true;popAudio.clip=popClip;
        RestartModule();
    }
    static Vector3[] Scales(Transform[] ts){var a=new Vector3[ts.Length];for(int i=0;i<a.Length;i++)a[i]=ts[i].localScale;return a;}
    static Vector3[] Positions(Transform[] ts){var a=new Vector3[ts.Length];for(int i=0;i<a.Length;i++)a[i]=ts[i].localPosition;return a;}
    static Quaternion[] Rotations(Transform[] ts){var a=new Quaternion[ts.Length];for(int i=0;i<a.Length;i++)a[i]=ts[i].localRotation;return a;}
    void ResetParts(Transform[] ts,Vector3[] p,Quaternion[] q){for(int i=0;i<ts.Length;i++){ts[i].localPosition=p[i];ts[i].localRotation=q[i];}}
    public void RestartModule() {
        if(sequence!=null){StopCoroutine(sequence);sequence=null;}
        RestoreInput();CurrentPhase=Phase.EnterPlayground;enteredAt=Time.time;
        child.localPosition=childPosition;child.localRotation=childRotation;ResetParts(upperArms,armPos,armRot);ResetParts(forearms,forePos,foreRot);ResetParts(hands,handPos,handRot);
        for(int i=0;i<upperArms.Length;i++){upperArms[i].localScale=armScale[i];forearms[i].localScale=foreScale[i];}
        for(int i=0;i<eyes.Length;i++)eyes[i].transform.localPosition=eyePos[i];
        if(burstBalloon)burstBalloon.SetActive(true);foreach(var s in balloonStrings)if(s)s.gameObject.SetActive(true);
        for(int i=0;i<3;i++){dwell[i]=0;noticed[i]=false;}
        if(guidedMarker)guidedMarker.SetActive(guidedPractice);
        partyAudio.volume=.12f;partyAudio.Play();popAudio.Stop();
        title.text="01  ENTER THE PLAYGROUND";
        instructions.text="You are at the playground with your child. Take a moment to look around and see how he is playing.\n\nFind your child at the yellow slide. Notice the birthday party, its speakers and balloons, and the aunty on the bench.\n\nPoint with your RIGHT controller and release the trigger to select. Move closer with your existing VR movement controls.";
        wristTitle.text="MODULE 1  /  STEP 1";
        actionLabel.text="Watch what changes";wristActionLabel.text="Watch what changes";
        restartButton.interactable=true;progress.value=0;
        Speak(entryVoice);
    }
    void Update() {
        if(CurrentPhase==Phase.EnterPlayground){
            float elapsed=Time.time-enteredAt;
            if(guidedMarker)guidedMarker.SetActive(guidedPractice&&elapsed<6f);
            Transform[] targets={child,partyTarget,onlooker};
            for(int i=0;i<3;i++)if(head&&!noticed[i]){
                Vector3 target=targets[i].position+Vector3.up*(i==1?1.6f:1f);
                if(Vector3.Angle(head.forward,target-head.position)<22f){
                    if(!Physics.Linecast(head.position,target,out RaycastHit hit)||hit.transform.IsChildOf(targets[i]))dwell[i]+=Time.deltaTime;
                }
                if(dwell[i]>=.65f)noticed[i]=true;
            }
            bool ready=ReadyForStepTwo;actionButton.interactable=ready;wristActionButton.interactable=ready;
            status.text=(ready?"Ready when you are. Move closer to your child, then select Watch what changes.":"Take a few seconds to look around...")+
                "\n"+(noticed[0]?"[Seen]":"[Look]")+" Child     "+(noticed[1]?"[Seen]":"[Look]")+" Party     "+(noticed[2]?"[Seen]":"[Look]")+" Aunty";
            wristStatus.text=ready?"Look around, then select below.":"Look around the playground...";
        }
    }
    void LateUpdate(){
        if(wristCanvas&&head){
            // Stay attached to the left controller; orient the panel toward the wearer.
            Vector3 away=wristCanvas.transform.position-head.position;
            if(away.sqrMagnitude>.001f)wristCanvas.transform.rotation=Quaternion.LookRotation(away,head.up);
        }
    }
    public void PrimaryAction(){if(ReadyForStepTwo)StartStepTwo();}
    public void StartStepTwo(){
        if(!ReadyForStepTwo)return;
        sequence=StartCoroutine(WatchEvent());
    }
    IEnumerator WatchEvent(){
        CurrentPhase=Phase.WatchChanges;if(guidedMarker)guidedMarker.SetActive(false);
        foreach(var b in lockDuringEvent)if(b){previousEnabled[b]=b.enabled;b.enabled=false;}
        actionButton.interactable=false;wristActionButton.interactable=false;restartButton.interactable=false;
        title.text="02  WATCH WHAT CHANGES";wristTitle.text="MODULE 1  /  STEP 2";
        actionLabel.text="Watching...";wristActionLabel.text="Watching...";
        instructions.text="Watch the scene. Interaction and controller movement are paused for this part. You can still look around naturally.\n\nSomething has changed. Watch your child closely. What do you notice in his body?";
        float started=Time.time;bool popped=false;
        while(Time.time-started<eventSeconds){
            float t=Time.time-started;
            if(!popped&&t>=4f){
                popped=true;if(burstBalloon)burstBalloon.SetActive(false);foreach(var s in balloonStrings)if(s)s.gameObject.SetActive(false);
                popAudio.Play();partyAudio.volume=.3f;SetDistressedPose();Speak(changeVoice);
            }
            progress.value=t/eventSeconds;
            int remaining=Mathf.CeilToInt(eventSeconds-t);
            status.text=(popped?"The balloon has burst and the party music is louder. Observe your child.":"Watch the playground...")+"\nObservation time remaining: "+remaining+" seconds";
            wristStatus.text="Observe your child  /  "+remaining+"s";
            yield return null;
        }
        progress.value=1;CurrentPhase=Phase.ObservationHold;sequence=null;RestoreInput();
        title.text="02  LOOK CLOSELY";wristTitle.text="MODULE 1  /  STEP 2 COMPLETE";
        instructions.text="The scene is holding at the foot of the slide. Look closely at your child's hands, posture and gaze.\n\nThis prototype uses a still pose instead of animation.\n\nSteps 1 and 2 are complete. Collect Clues is the next step in the original plan and will be added later.";
        status.text="Observation complete. You can move and look closely again.";
        wristStatus.text="Scene held for observation.";
        actionLabel.text="Collect clues - later";wristActionLabel.text="Collect clues - later";
        restartButton.interactable=true;
    }
    void SetDistressedPose(){
        // One immediate pose change; no animator, rocking or tweening.
        Vector3 toward=partyAudio.transform.position-child.position;toward.y=0;
        if(toward.sqrMagnitude>.1f)child.rotation=Quaternion.LookRotation(-toward,Vector3.up);
        for(int i=0;i<upperArms.Length;i++){
            float side=i==0?-1:1;
            SetSegment(upperArms[i],new Vector3(side*.29f,1.36f,0),new Vector3(side*.43f,1.52f,-.05f));
            SetSegment(forearms[i],new Vector3(side*.43f,1.52f,-.05f),new Vector3(side*.205f,1.76f,-.015f));
            hands[i].localPosition=new Vector3(side*.205f,1.76f,-.015f);
        }
        for(int i=0;i<eyes.Length;i++){Vector3 p=eyePos[i];p.x+=.018f;eyes[i].transform.localPosition=p;}
    }
    static void SetSegment(Transform t,Vector3 a,Vector3 b){t.localPosition=(a+b)*.5f;t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);var s=t.localScale;s.y=Vector3.Distance(a,b)*.5f;t.localScale=s;}
    void RestoreInput(){foreach(var kv in previousEnabled)if(kv.Key)kv.Key.enabled=kv.Value;previousEnabled.Clear();}
    void Speak(AudioClip clip){if(narration&&clip){narration.Stop();narration.clip=clip;narration.Play();}}
    static AudioClip BuildMusic(){
        const int rate=22050;float[] samples=new float[rate*8];float[] notes={261.63f,329.63f,392f,329.63f,293.66f,349.23f,440f,349.23f,329.63f,392f,523.25f,392f,293.66f,349.23f,392f,293.66f};
        for(int i=0;i<samples.Length;i++){float t=i/(float)rate;float u=t%.5f;float env=Mathf.Min(u*25,1)*Mathf.Exp(-u*5);float f=notes[(int)(t*2)%notes.Length];float melody=Mathf.Sin(2*Mathf.PI*f*t)*.17f*env;float bass=Mathf.Sin(2*Mathf.PI*(t<4?130.81f:146.83f)*t)*.08f;float beat=Mathf.Sin(2*Mathf.PI*70*t)*Mathf.Exp(-(t%.5f)*30)*.1f;samples[i]=melody+bass+beat;}
        var clip=AudioClip.Create("Prototype birthday music",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
    }
    static AudioClip BuildPop(){
        const int rate=22050;float[] samples=new float[(int)(rate*.35f)];System.Random rng=new System.Random(19);
        for(int i=0;i<samples.Length;i++){float t=i/(float)rate;samples[i]=(float)(rng.NextDouble()*2-1)*Mathf.Exp(-t*32)*.55f;}
        var clip=AudioClip.Create("Prototype balloon pop",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
    }
    void OnDisable(){RestoreInput();}
    void OnDestroy(){if(musicClip)Destroy(musicClip);if(popClip)Destroy(popClip);}
}
}
