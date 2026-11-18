using System;using TMPro;using UnityEngine;using UnityEngine.Events;using UnityEngine.UI;
using BitsPilani.Module1.Playground;
namespace BitsPilani.Module4 {
    [Serializable] public sealed class Module4Choice {public Button button;public TMP_Text label;}
    [Serializable] public sealed class Module4ReviewRow {public RectTransform root;public TMP_Text value,detail;public LayoutElement layout;public float minimumHeight=112;}
    public sealed class Module4UI : MonoBehaviour {
        [Header("Scene-authored main panel - all styling stays editable")]
        public Canvas mainCanvas;public TMP_Text title,body,stepLabel,status,footer,hint;
        public Image statusBackground;public Slider progress;
        public Button primary,secondary;
        [Header("Whole-step skip - scene-authored header button")]
        public Button skipStep;public TMP_Text primaryLabel,secondaryLabel;
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
        [Header("Child-view return - scene authored, follows camera Y rotation only")]
        [Tooltip("Keeps the authored placement and tilt, follows left/right camera rotation only, and stays at a fixed height while visible.")]
        public Canvas childReturnCanvas;public Button childReturnButton;
        Vector3 returnCameraOffset;Quaternion returnCameraRotation;
        float returnCameraHeight;bool returnPoseCaptured;
        [Header("Participant ID and virtual keyboard")]
        public Canvas idCanvas;public TMP_InputField idInput;public Button idDone;
        public Module4ParticipantKeyboard keyboard;
        [Header("Review dashboard")]
        public GameObject dashboard;
        [Min(1),Tooltip("Number of clue rows visible before scrolling on the review/summary tables. Expands height only; widths and row styling stay unchanged. Normal step layout is restored afterwards.")]
        public int reviewVisibleClueRows=3;
        sealed class ReviewRectState {public RectTransform rect;public Vector2 size,position;}
        readonly System.Collections.Generic.List<ReviewRectState> reviewRectStates=new System.Collections.Generic.List<ReviewRectState>();
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
        bool bodyStyleChanged;FontStyles savedBodyStyle;FontWeight savedBodyWeight;
        public void Bind(Module4SceneController flow){
            owner=flow;CaptureReturnCameraPose();
            if(skipStep){skipStep.onClick.RemoveAllListeners();skipStep.onClick.AddListener(flow.SkipCurrentStep);skipStep.gameObject.SetActive(true);}
            if(clueTool){clueTool.onClick.RemoveAllListeners();clueTool.onClick.AddListener(flow.OpenClueTool);}
            if(guessTool){guessTool.onClick.RemoveAllListeners();guessTool.onClick.AddListener(flow.OpenGuessTool);}
            if(idDone){idDone.onClick.RemoveAllListeners();idDone.onClick.AddListener(()=>flow.AcceptParticipantId(idInput?idInput.text:""));}
            if(idInput){idInput.onValueChanged.AddListener(_=>RefreshId());}
            if(keyboard){keyboard.owner=flow;keyboard.Bind();}
            if(narrativeScrollbar)narrativeScrollbar.onValueChanged.AddListener(v=>narrative.verticalNormalizedPosition=v);
            if(childReturnButton){childReturnButton.onClick.RemoveAllListeners();childReturnButton.onClick.AddListener(flow.ReturnToWalk);}
            if(toolsCanvas)toolsCanvas.gameObject.SetActive(false);SetChildReturn(false);RefreshId();RefreshFeedback();
        }
        public void RefreshId(){bool valid=idInput&&Module4Session.IsValidId(idInput.text);if(idDone)idDone.interactable=valid;if(keyboard)keyboard.Refresh();RefreshFeedback();}
        public void ShowId(bool visible){
            if(idCanvas)idCanvas.gameObject.SetActive(visible);if(mainCanvas)mainCanvas.gameObject.SetActive(!visible);
            if(!visible&&keyboard)keyboard.Close();RefreshId();
        }
        public void Step(int number,string caption){if(stepLabel)stepLabel.text="MODULE 4  /  STEP "+number+" OF 8";if(toolsStep)toolsStep.text=caption;if(progress)progress.value=(number-1)/8f;}
        public void Show(string heading,string text,string state="",bool split=false){
            RestoreReviewHeight();RestoreBodyStyle();IsShowingReflection=false;mainCanvas.gameObject.SetActive(true);
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
        public void Tools(bool clues,bool guesses){if(toolsCanvas)toolsCanvas.gameObject.SetActive(false);if(clueTool)clueTool.interactable=clues;if(guessTool)guessTool.interactable=guesses;RefreshFeedback();}
        public void Busy(){foreach(var b in mainCanvas.GetComponentsInChildren<Button>(true))if(!b.GetComponentInParent<BitsPilani.Shared.ModuleHomeConfirmation>(true))b.interactable=false;Tools(false,false);RefreshFeedback();}
        public void SetChildReturn(bool visible){
            if(!returnPoseCaptured)CaptureReturnCameraPose();
            // Anchor height at child-view activation, not the earlier parent eye height.
            if(visible&&childReturnCanvas&&!childReturnCanvas.gameObject.activeSelf&&owner&&owner.rig&&owner.rig.head)
                returnCameraHeight=owner.rig.head.position.y;
            if(childReturnCanvas)childReturnCanvas.gameObject.SetActive(visible);
            if(childReturnButton)childReturnButton.interactable=visible;
            UpdateReturnCameraPose();
        }
        void CaptureReturnCameraPose(){
            if(returnPoseCaptured||!childReturnCanvas||!owner||!owner.rig||!owner.rig.head)return;
            var camera=owner.rig.head;var target=childReturnCanvas.transform;
            Quaternion yaw=Quaternion.Euler(0,camera.eulerAngles.y,0);
            Quaternion basis=target.IsChildOf(camera)?camera.rotation:yaw;
            returnCameraOffset=Quaternion.Inverse(basis)*(target.position-camera.position);
            returnCameraRotation=Quaternion.Inverse(basis)*target.rotation;
            returnCameraHeight=camera.position.y;returnPoseCaptured=true;
        }
        void OnEnable(){Application.onBeforeRender+=UpdateReturnCameraPose;}
        void OnDisable(){Application.onBeforeRender-=UpdateReturnCameraPose;}
        void LateUpdate(){UpdateReturnCameraPose();}
        void UpdateReturnCameraPose(){
            if(!returnPoseCaptured||!childReturnCanvas||!childReturnCanvas.gameObject.activeInHierarchy||!owner||!owner.rig||!owner.rig.head)return;
            var camera=owner.rig.head;Quaternion yaw=Quaternion.Euler(0,camera.eulerAngles.y,0);
            Vector3 origin=camera.position;origin.y=returnCameraHeight;
            childReturnCanvas.transform.SetPositionAndRotation(origin+yaw*returnCameraOffset,yaw*returnCameraRotation);
        }
        public void Reflection(string question,int number){
            Show("Reflect on what changed",question,"Record at least one answer before moving on.",true);
            IsShowingReflection=true;Question=number;recordingGroup.SetActive(true);
            recordAnswer.onClick.RemoveAllListeners();recordAnswer.onClick.AddListener(owner.ToggleAnswerRecording);
            nextAnswer.onClick.RemoveAllListeners();nextAnswer.onClick.AddListener(owner.NextReflectionQuestion);
            Layout(true);RefreshRecording();
        }
        float nextRecordingRefresh;
        void Update(){RefreshSkipButton();if(IsShowingReflection&&Application.isPlaying&&Time.unscaledTime>=nextRecordingRefresh){nextRecordingRefresh=Time.unscaledTime+.1f;RefreshRecording();}}
        public void RefreshRecording(){
            var session=Module4Session.Instance;if(!session||!owner||!owner.answers)return;
            var r=owner.answers;int count=session.Data.answers.FindAll(a=>a.question==Question).Count;
            bool waiting=session.IsNarrating||owner.ReflectionVoicePending;
            recordAnswer.interactable=!waiting&&!r.IsStarting;
            recordLabel.text=waiting?"Please listen...":r.IsStarting?"Starting...":r.IsRecording?"Save recording":count>0?"Record another answer":"Record answer";
            nextAnswer.interactable=!waiting&&!r.IsRecording&&!r.IsStarting&&(!session.definition.requireSavedAnswer||session.HasAnswer(Question));
            nextLabel.text="Next";
            questionCount.text="QUESTION "+Question+" OF 3   |   "+count+" clip"+(count==1?"":"s")+" saved";
            status.text=waiting?"The Record button becomes available when the question VO ends.":r.Status;
            footer.text=r.IsRecording?"Your microphone answer is being recorded.":"You can save more than one clip for this question.";
            RefreshFeedback();
        }
        void RefreshSkipButton(){
            if(!skipStep||!owner)return;
            bool available=owner.CanSkipCurrentStep;
            if(skipStep.interactable!=available){skipStep.interactable=available;var feedback=skipStep.GetComponent<Module4ButtonFeedback>();if(feedback)feedback.RefreshAppearance();}
        }
        public void FormatGuessReviewText(){
            if(!body)return;
            if(!bodyStyleChanged){savedBodyStyle=body.fontStyle;savedBodyWeight=body.fontWeight;bodyStyleChanged=true;}
            body.fontStyle&=~FontStyles.Bold;body.fontWeight=FontWeight.Regular;body.ForceMeshUpdate();
        }
        void RestoreBodyStyle(){if(!bodyStyleChanged||!body)return;body.fontStyle=savedBodyStyle;body.fontWeight=savedBodyWeight;bodyStyleChanged=false;}
        public void AttemptReview(Module4RunData data,Module4Definition definition){
            Show("Review your first attempt","","");Step(7,"Review");
            narrative.gameObject.SetActive(false);status.gameObject.SetActive(false);statusBackground.gameObject.SetActive(false);
            dashboard.SetActive(true);firstTable.SetActive(true);compareTable.SetActive(false);
            clueCount.text=data.clues.Count+" selected";guessCount.text=data.guesses.Count+" saved";
            noClues.text=noCluesText;noClues.gameObject.SetActive(data.clues.Count==0);
            noGuesses.text=noGuessesText;noGuesses.gameObject.SetActive(data.guesses.Count==0);
            Fill(clueRows,clueScroll,data.clues.Count,i=>(i+1)+". "+data.clues[i],null);
            Fill(guessRows,guessScroll,data.guesses.Count,i=>(i+1)+". "+data.guesses[i].idea,i=>"Confidence: "+definition.confidence[Mathf.Clamp(data.guesses[i].confidence,0,2)]);
            ExpandReviewHeight();
            firstAction.text=data.firstResponse;firstOutcome.text=data.firstOutcome;
            firstTiming.text=data.firstResponseSeconds<0?"Not observed":"During the walk";
            footer.text="Clues: "+data.clues.Count+"  |  Ideas: "+data.guesses.Count+"  |  Answer clips: "+data.answers.Count;
            hint.text=defaultHint;
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
            ExpandReviewHeight();
            compareFirstAction.text=data.firstResponse;compareFirstOutcome.text=data.firstOutcome;compareFirstTiming.text=data.firstResponseIndex<0?"Not recorded":"During the walk";
            replayAction.text=data.replayResponse;replayOutcome.text=data.replayOutcome;replayTiming.text=data.expectationsSet?"At home, before leaving":"Not recorded";
            footer.text="Clues: "+data.clues.Count+"  |  Ideas: "+data.guesses.Count+"  |  Answer clips: "+data.answers.Count;
            hint.text=defaultHint;
        }
        void RestoreReviewHeight(){
            foreach(var state in reviewRectStates)if(state.rect){state.rect.sizeDelta=state.size;state.rect.anchoredPosition=state.position;}
            reviewRectStates.Clear();
        }
        void ResizeReviewRect(RectTransform rect,float height,float shift){
            if(!rect)return;
            foreach(var existing in reviewRectStates)if(existing.rect==rect)return;
            reviewRectStates.Add(new ReviewRectState{rect=rect,size=rect.sizeDelta,position=rect.anchoredPosition});
            rect.sizeDelta+=new Vector2(0,height);rect.anchoredPosition+=new Vector2(0,shift);
        }
        void ExpandReviewCard(ScrollRect scroll,float extra){
            if(!scroll)return;
            ResizeReviewRect(scroll.transform as RectTransform,extra,0);
            // These authored viewports use fixed heights rather than stretched anchors.
            if(scroll.viewport)ResizeReviewRect(scroll.viewport,Mathf.Approximately(scroll.viewport.anchorMin.y,scroll.viewport.anchorMax.y)?extra:0,0);
            var bar=scroll.verticalScrollbar?scroll.verticalScrollbar.transform as RectTransform:null;
            if(bar)ResizeReviewRect(bar,Mathf.Approximately(bar.anchorMin.y,bar.anchorMax.y)?extra:0,0);
            foreach(Transform child in scroll.transform){
                var rect=child as RectTransform;
                if(!rect||rect==scroll.viewport||rect==bar)continue;
                if(rect.anchoredPosition.y>0&&Mathf.Approximately(rect.anchorMin.y,.5f)&&Mathf.Approximately(rect.anchorMax.y,.5f))
                    ResizeReviewRect(rect,0,extra*.5f);
            }
        }
        void ExpandReviewHeight(){
            if(!clueScroll||!clueScroll.viewport||clueRows==null||clueRows.Length==0)return;
            Canvas.ForceUpdateCanvases();
            int visible=Mathf.Min(Mathf.Max(1,reviewVisibleClueRows),clueRows.Length);
            var layout=clueScroll.content.GetComponent<VerticalLayoutGroup>();
            float needed=layout?layout.padding.top+layout.padding.bottom+layout.spacing*(visible-1):0;
            for(int i=0;i<visible;i++)needed+=Mathf.Max(clueRows[i].minimumHeight,clueRows[i].layout?clueRows[i].layout.preferredHeight:clueRows[i].root.rect.height);
            float extra=Mathf.Max(0,needed-clueScroll.viewport.rect.height);
            if(extra>0){
                // Height only. Keep the original table rows intact and create space
                // around them instead of squeezing the response table or shrinking text.
                var canvasRect=(RectTransform)mainCanvas.transform;
                ResizeReviewRect(canvasRect,extra,0);
                var panel=title.transform.parent as RectTransform;
                if(panel&&panel!=canvasRect)ResizeReviewRect(panel,Mathf.Approximately(panel.anchorMin.y,panel.anchorMax.y)?extra:0,0);
                ResizeReviewRect(dashboard.transform as RectTransform,extra,0);
                ExpandReviewCard(clueScroll,extra);
                ExpandReviewCard(guessScroll,extra);
                if(firstTable)ResizeReviewRect(firstTable.transform as RectTransform,0,-extra*.5f);
                if(compareTable)ResizeReviewRect(compareTable.transform as RectTransform,0,-extra*.5f);
                // Keep the authored gold trim aligned with the lifted header.
                var accent=panel?panel.Find("Gold accent") as RectTransform:null;
                if(accent&&Mathf.Approximately(accent.anchorMin.y,.5f)&&Mathf.Approximately(accent.anchorMax.y,.5f))
                    ResizeReviewRect(accent,0,extra*.5f);
                ResizeReviewRect(title.rectTransform,0,extra*.5f);
                if(stepLabel)ResizeReviewRect(stepLabel.rectTransform,0,extra*.5f);
                if(skipStep)ResizeReviewRect(skipStep.transform as RectTransform,0,extra*.5f);
                if(footer)ResizeReviewRect(footer.rectTransform,0,-extra*.5f);
                if(hint)ResizeReviewRect(hint.rectTransform,0,-extra*.5f);
                if(progress)ResizeReviewRect(progress.transform as RectTransform,0,-extra*.5f);
                if(primary)ResizeReviewRect(primary.transform as RectTransform,0,-extra*.5f);
                if(secondary)ResizeReviewRect(secondary.transform as RectTransform,0,-extra*.5f);
            }
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(clueScroll.content);
            ScrollVisibility(clueScroll,clueScroll.verticalScrollbar);
            if(guessScroll){LayoutRebuilder.ForceRebuildLayoutImmediate(guessScroll.content);ScrollVisibility(guessScroll,guessScroll.verticalScrollbar);}
        }
        void Fill(Module4ReviewRow[] rows,ScrollRect scroll,int count,Func<int,string> value,Func<int,string> detail){
            for(int i=0;i<rows.Length;i++){var row=rows[i];row.root.gameObject.SetActive(i<count);if(i>=count)continue;row.value.text=value(i);if(row.detail)row.detail.text=detail==null?"":detail(i);}
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            for(int i=0;i<Mathf.Min(count,rows.Length);i++){var row=rows[i];float h=row.value.GetPreferredValues(row.value.text,row.value.rectTransform.rect.width,Mathf.Infinity).y+12;if(row.detail)h+=row.detail.GetPreferredValues(row.detail.text,row.detail.rectTransform.rect.width,Mathf.Infinity).y+6;row.layout.preferredHeight=Mathf.Max(row.minimumHeight,h);}
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);scroll.verticalNormalizedPosition=1;ScrollVisibility(scroll,scroll.verticalScrollbar);
        }
        void ScrollVisibility(ScrollRect scroll,Scrollbar bar){bool overflow=scroll.content.rect.height>scroll.viewport.rect.height+1;scroll.vertical=overflow;if(bar){bar.gameObject.SetActive(overflow);bar.interactable=overflow;bar.size=Mathf.Clamp01(scroll.viewport.rect.height/Mathf.Max(1,scroll.content.rect.height));var cg=bar.GetComponent<CanvasGroup>();if(cg){cg.alpha=overflow?1:0;cg.blocksRaycasts=overflow;cg.interactable=overflow;}}}
        public void RefreshFeedback(){
            // Home confirmation owns its button gates and captures its scene-authored colors.
            // Step refreshes must not paint a hidden popup as disabled before it opens.
            foreach(var f in GetComponentsInChildren<Module4ButtonFeedback>(true))if(!f.GetComponentInParent<BitsPilani.Shared.ModuleHomeConfirmation>(true))f.RefreshAppearance();
            if(mainCanvas)foreach(var f in mainCanvas.GetComponentsInChildren<Module4ButtonFeedback>(true))if(!f.GetComponentInParent<BitsPilani.Shared.ModuleHomeConfirmation>(true))f.RefreshAppearance();
        }
    }
}

