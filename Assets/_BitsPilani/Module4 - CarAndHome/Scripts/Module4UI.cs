using System;using TMPro;using UnityEngine;using UnityEngine.Events;using UnityEngine.UI;
using BitsPilani.Module1.Playground;
namespace BitsPilani.Module4 {
    [Serializable] public sealed class Module4Choice {public Button button;public TMP_Text label;}
    [Serializable] public sealed class Module4ReviewRow {public RectTransform root;public TMP_Text value,detail;public LayoutElement layout;public float minimumHeight=112;}
    public sealed class Module4UI : MonoBehaviour {
        [Header("Scene-authored main panel - all styling stays editable")]
        public Canvas mainCanvas;public TMP_Text title,body,stepLabel,status,footer,hint;
        public Image statusBackground;public Slider progress;
        public Button primary,secondary;public TMP_Text primaryLabel,secondaryLabel;
        public Module4Choice[] choices;
        [Header("Editable narrative layouts")]
        public ScrollRect narrative;public Scrollbar narrativeScrollbar;
        public Vector2 fullSize=new Vector2(1740,500),fullPosition=new Vector2(0,80);
        public Vector2 choiceSize=new Vector2(820,500),choicePosition=new Vector2(-465,80);
        [Header("Optional controller clue and guess tools")]
        public Canvas toolsCanvas;public Button clueTool,guessTool;public TMP_Text toolsStep;
        [Header("Reflection recording UI")]
        public GameObject recordingGroup;public Button recordAnswer,nextAnswer;
        public TMP_Text recordLabel,nextLabel,questionCount;
        [Header("Child-view return - scene authored, camera attached")]
        public Canvas childReturnCanvas;public Button childReturnButton;
        [Header("Participant ID and virtual keyboard")]
        public Canvas idCanvas;public TMP_InputField idInput;public Button idDone;
        public Module4ParticipantKeyboard keyboard;
        [Header("Review dashboard")]
        public GameObject dashboard;
        public TMP_Text clueCount,guessCount,noClues,noGuesses;
        public Module4ReviewRow[] clueRows,guessRows;
        public ScrollRect clueScroll,guessScroll;
        public GameObject firstTable,compareTable;
        public TMP_Text firstAction,firstOutcome,firstTiming,replayAction,replayOutcome,replayTiming;
        public TMP_Text compareFirstAction,compareFirstOutcome,compareFirstTiming;
        [Header("Editable help text")]
        [TextArea] public string defaultHint="Point with the right-controller ray and release the trigger to select.";
        [TextArea] public string noCluesText="No clues selected";
        [TextArea] public string noGuessesText="No guesses selected";
        public bool IsShowingReflection {get;private set;}
        public int Question {get;private set;}
        public int VisibleChoiceCount {get;private set;}
        Module4SceneController owner;
        public void Bind(Module4SceneController flow){
            owner=flow;
            if(clueTool){clueTool.onClick.RemoveAllListeners();clueTool.onClick.AddListener(flow.OpenClueTool);}
            if(guessTool){guessTool.onClick.RemoveAllListeners();guessTool.onClick.AddListener(flow.OpenGuessTool);}
            if(idDone){idDone.onClick.RemoveAllListeners();idDone.onClick.AddListener(()=>flow.AcceptParticipantId(idInput?idInput.text:""));}
            if(idInput){idInput.onValueChanged.AddListener(_=>RefreshId());}
            if(keyboard){keyboard.owner=flow;keyboard.Bind();}
            if(narrativeScrollbar)narrativeScrollbar.onValueChanged.AddListener(v=>narrative.verticalNormalizedPosition=v);
            if(childReturnButton){childReturnButton.onClick.RemoveAllListeners();childReturnButton.onClick.AddListener(flow.ReturnToWalk);}
            SetChildReturn(false);RefreshId();RefreshFeedback();
        }
        public void RefreshId(){bool valid=idInput&&Module4Session.IsValidId(idInput.text);if(idDone)idDone.interactable=valid;if(keyboard)keyboard.Refresh();RefreshFeedback();}
        public void ShowId(bool visible){
            if(idCanvas)idCanvas.gameObject.SetActive(visible);if(mainCanvas)mainCanvas.gameObject.SetActive(!visible);
            if(!visible&&keyboard)keyboard.Close();RefreshId();
        }
        public void Step(int number,string caption){if(stepLabel)stepLabel.text="MODULE 4  /  STEP "+number+" OF 8";if(toolsStep)toolsStep.text=caption;if(progress)progress.value=(number-1)/8f;}
        public void Show(string heading,string text,string state="",bool split=false){
            IsShowingReflection=false;mainCanvas.gameObject.SetActive(true);
            if(dashboard)dashboard.SetActive(false);
            if(narrative)narrative.gameObject.SetActive(true);
            if(status)status.gameObject.SetActive(true);if(statusBackground)statusBackground.gameObject.SetActive(!string.IsNullOrEmpty(state));
            title.text=heading;body.text=text;status.text=state;footer.text="";hint.text=defaultHint;
            if(recordingGroup)recordingGroup.SetActive(false);
            Draw(Array.Empty<string>(),null,null);Layout(split);
            Primary("",null,false,false);Secondary("",null,false,false);
        }
        public void Layout(bool split){if(!narrative)return;var rt=(RectTransform)narrative.transform;rt.sizeDelta=split?choiceSize:fullSize;rt.anchoredPosition=split?choicePosition:fullPosition;RefreshNarrative();}
        public void RefreshNarrative(){
            if(!narrative||!body)return;Canvas.ForceUpdateCanvases();
            float h=Mathf.Max(narrative.viewport.rect.height,body.GetPreferredValues(body.text,Mathf.Max(100,body.rectTransform.rect.width),Mathf.Infinity).y+24);
            body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,h);body.rectTransform.anchoredPosition=new Vector2(body.rectTransform.anchoredPosition.x,0);
            Canvas.ForceUpdateCanvases();narrative.verticalNormalizedPosition=1;ScrollVisibility(narrative,narrativeScrollbar);
        }
        public void Primary(string text,UnityAction action,bool enabled=true,bool visible=true){primary.gameObject.SetActive(visible);primaryLabel.text=text;primary.onClick.RemoveAllListeners();if(action!=null)primary.onClick.AddListener(action);primary.interactable=enabled&&action!=null;RefreshFeedback();}
        public void Secondary(string text,UnityAction action,bool enabled=true,bool visible=true){secondary.gameObject.SetActive(visible);secondaryLabel.text=text;secondary.onClick.RemoveAllListeners();if(action!=null)secondary.onClick.AddListener(action);secondary.interactable=enabled&&action!=null;RefreshFeedback();}
        public void Draw(string[] labels,bool[] marked,Action<int> callback){
            VisibleChoiceCount=Mathf.Min(labels.Length,choices.Length);
            for(int i=0;i<choices.Length;i++){var c=choices[i];c.button.gameObject.SetActive(i<VisibleChoiceCount);c.button.onClick.RemoveAllListeners();if(i>=VisibleChoiceCount)continue;c.label.text=labels[i];c.button.interactable=callback!=null;int index=i;if(callback!=null)c.button.onClick.AddListener(()=>callback(index));var f=c.button.GetComponent<Module4ButtonFeedback>();if(f)f.SetChoiceSelected(marked!=null&&i<marked.Length&&marked[i]);}
            RefreshFeedback();if(VisibleChoiceCount>0)Layout(true);
        }
        public void Tools(bool clues,bool guesses){if(toolsCanvas)toolsCanvas.gameObject.SetActive(clues||guesses);if(clueTool)clueTool.interactable=clues;if(guessTool)guessTool.interactable=guesses;RefreshFeedback();}
        public void Busy(){foreach(var b in mainCanvas.GetComponentsInChildren<Button>(true))b.interactable=false;Tools(false,false);RefreshFeedback();}
        public void SetChildReturn(bool visible){if(childReturnCanvas)childReturnCanvas.gameObject.SetActive(visible);if(childReturnButton)childReturnButton.interactable=visible;}
        public void Reflection(string question,int number){
            Show("Reflect on what changed",question,"Record at least one answer before moving on.",true);
            IsShowingReflection=true;Question=number;recordingGroup.SetActive(true);
            recordAnswer.onClick.RemoveAllListeners();recordAnswer.onClick.AddListener(owner.ToggleAnswerRecording);
            nextAnswer.onClick.RemoveAllListeners();nextAnswer.onClick.AddListener(owner.NextReflectionQuestion);
            Layout(true);RefreshRecording();
        }
        float nextRecordingRefresh;
        void Update(){if(IsShowingReflection&&Application.isPlaying&&Time.unscaledTime>=nextRecordingRefresh){nextRecordingRefresh=Time.unscaledTime+.1f;RefreshRecording();}}
        public void RefreshRecording(){
            var session=Module4Session.Instance;if(!session||!owner||!owner.answers)return;
            var r=owner.answers;int count=session.Data.answers.FindAll(a=>a.question==Question).Count;
            bool waiting=session.IsNarrating;
            recordAnswer.interactable=!waiting&&!r.IsStarting;
            recordLabel.text=waiting?"Please listen...":r.IsStarting?"Starting...":r.IsRecording?"Save recording":count>0?"Record another answer":"Record answer";
            nextAnswer.interactable=!waiting&&!r.IsRecording&&!r.IsStarting&&(!session.definition.requireSavedAnswer||session.HasAnswer(Question));
            nextLabel.text="Next";
            questionCount.text="QUESTION "+Question+" OF 3   |   "+count+" clip"+(count==1?"":"s")+" saved";
            status.text=waiting?"The Record button becomes available when the question VO ends.":r.Status;
            footer.text=r.IsRecording?"Your microphone answer is being recorded.":"You can save more than one clip for this question.";
            RefreshFeedback();
        }
        public void Summary(Module4RunData data,Module4Definition definition){
            Show("First attempt and replay","","");Step(8,"Summary");if(progress)progress.value=1;
            narrative.gameObject.SetActive(false);status.gameObject.SetActive(false);statusBackground.gameObject.SetActive(false);
            dashboard.SetActive(true);firstTable.SetActive(false);compareTable.SetActive(true);
            clueCount.text=data.clues.Count+" selected";guessCount.text=data.guesses.Count+" saved";
            noClues.text=noCluesText;noClues.gameObject.SetActive(data.clues.Count==0);
            noGuesses.text=noGuessesText;noGuesses.gameObject.SetActive(data.guesses.Count==0);
            Fill(clueRows,clueScroll,data.clues.Count,i=>(i+1)+". "+data.clues[i],null);
            Fill(guessRows,guessScroll,data.guesses.Count,i=>(i+1)+". "+data.guesses[i].idea,i=>"Confidence: "+definition.confidence[Mathf.Clamp(data.guesses[i].confidence,0,2)]);
            compareFirstAction.text=data.firstResponse;compareFirstOutcome.text=data.firstOutcome;compareFirstTiming.text="During the walk, after he became upset";
            replayAction.text=data.replayResponse;replayOutcome.text=data.replayOutcome;replayTiming.text="At home, before leaving";
            footer.text="Clues: "+data.clues.Count+"  |  Ideas: "+data.guesses.Count+"  |  Answer clips: "+data.answers.Count;
            hint.text="You knew the plan. What helped your child know it too?";
        }
        void Fill(Module4ReviewRow[] rows,ScrollRect scroll,int count,Func<int,string> value,Func<int,string> detail){
            for(int i=0;i<rows.Length;i++){var row=rows[i];row.root.gameObject.SetActive(i<count);if(i>=count)continue;row.value.text=value(i);if(row.detail)row.detail.text=detail==null?"":detail(i);}
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            for(int i=0;i<Mathf.Min(count,rows.Length);i++){var row=rows[i];float h=row.value.GetPreferredValues(row.value.text,row.value.rectTransform.rect.width,Mathf.Infinity).y+12;if(row.detail)h+=row.detail.GetPreferredValues(row.detail.text,row.detail.rectTransform.rect.width,Mathf.Infinity).y+6;row.layout.preferredHeight=Mathf.Max(row.minimumHeight,h);}
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);scroll.verticalNormalizedPosition=1;ScrollVisibility(scroll,scroll.verticalScrollbar);
        }
        void ScrollVisibility(ScrollRect scroll,Scrollbar bar){bool overflow=scroll.content.rect.height>scroll.viewport.rect.height+1;scroll.vertical=overflow;if(bar){bar.gameObject.SetActive(overflow);bar.interactable=overflow;bar.size=Mathf.Clamp01(scroll.viewport.rect.height/Mathf.Max(1,scroll.content.rect.height));var cg=bar.GetComponent<CanvasGroup>();if(cg){cg.alpha=overflow?1:0;cg.blocksRaycasts=overflow;cg.interactable=overflow;}}}
        public void RefreshFeedback(){foreach(var f in GetComponentsInChildren<Module4ButtonFeedback>(true))f.RefreshAppearance();if(mainCanvas)foreach(var f in mainCanvas.GetComponentsInChildren<Module4ButtonFeedback>(true))f.RefreshAppearance();}
    }
}

