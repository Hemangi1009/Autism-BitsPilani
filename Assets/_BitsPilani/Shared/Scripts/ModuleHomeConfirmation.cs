using System;
using System.Collections;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using BitsPilani.Module1.Playground;
using BitsPilani.Module4;

namespace BitsPilani.Shared {
    // Scene-authored controls only. Opening/closing the popup never pauses or changes the module.
    public sealed class ModuleHomeConfirmation : MonoBehaviour {
        [Header("Scene-authored Home confirmation")]
        public Button homeButton,yesButton,noButton;
        [Tooltip("Assign your Home sprite on this child Image component in the scene.")]
        public Image homeIcon;
        public GameObject confirmationPanel,modalBlocker;
        public TMP_Text exitStatus;
        [Header("Current module - assign only the matching flow")]
        public PlaygroundFlowController module1;
        public Module4SceneController module4;
        public string entrySceneName="Autism_ModuleEntry";
        [Header("Header alignment - preserves authored offsets when review height changes")]
        public RectTransform skipAnchor;
        public bool followSkipVerticalPosition=true;
        [Header("Editable exit messages")]
        public string savingMessage="Saving and returning...";
        public string unavailableMessage="Module selection is not included in this build.";
        float homeHeaderOffset;
        Vector2 popupOffset;
        bool exiting,placed;
        public bool IsConfirming => confirmationPanel&&confirmationPanel.activeSelf;
        public bool IsExiting => exiting;
        void Awake(){
            if(homeButton)homeButton.onClick.AddListener(OpenConfirmation);
            if(yesButton)yesButton.onClick.AddListener(ConfirmYes);
            if(noButton)noButton.onClick.AddListener(ConfirmNo);
            CapturePlacement();SetPopup(false);
        }
        void CapturePlacement(){
            if(placed||!homeButton||!skipAnchor||!confirmationPanel)return;
            var home=(RectTransform)homeButton.transform;
            homeHeaderOffset=home.anchoredPosition.y-skipAnchor.anchoredPosition.y;
            popupOffset=((RectTransform)confirmationPanel.transform).anchoredPosition-home.anchoredPosition;
            placed=true;
        }
        bool CanExitNow(){
            if(exiting)return false;
            if(module4){
                var session=Module4Session.Instance;
                if(module4.IsTransitioning||(session&&session.PerspectiveTransitioning)||(module4.answers&&module4.answers.IsStarting))return false;
            }
            if(module1&&module1.recording&&module1.recording.IsSaving)return false;
            return module1||module4;
        }
        void LateUpdate(){
            CapturePlacement();
            if(placed&&followSkipVerticalPosition){
                var home=(RectTransform)homeButton.transform;
                home.anchoredPosition=new Vector2(home.anchoredPosition.x,skipAnchor.anchoredPosition.y+homeHeaderOffset);
                ((RectTransform)confirmationPanel.transform).anchoredPosition=home.anchoredPosition+popupOffset;
            }
            // Existing step Busy() gates include all canvas buttons. Keep only the new
            // Home controls available when exit is safe; the modal blocks the step below.
            bool ready=CanExitNow();
            if(homeButton)homeButton.interactable=ready;
            if(yesButton)yesButton.interactable=ready;
            if(noButton)noButton.interactable=!exiting;
        }
        void SetPopup(bool visible){
            if(confirmationPanel)confirmationPanel.SetActive(visible);
            if(modalBlocker)modalBlocker.SetActive(visible);
            if(!visible&&exitStatus)exitStatus.text="";
        }
        public void OpenConfirmation(){
            if(!CanExitNow())return;
            SetPopup(true);Debug.Log("[ModuleHome] Confirmation opened; module continues.",this);
        }
        public void ConfirmNo(){
            if(exiting)return;
            SetPopup(false);Debug.Log("[ModuleHome] Cancelled; flow and recording unchanged.",this);
        }
        public void ConfirmYes(){
            if(!IsConfirming||!CanExitNow())return;
            string scene=FolderManager.Instance?FolderManager.Instance.entrySceneName:entrySceneName;
            if(!Application.CanStreamedLevelBeLoaded(scene)){
                if(exitStatus)exitStatus.text=unavailableMessage;
                Debug.LogError("[ModuleHome] Cannot load selection scene: "+scene,this);return;
            }
            exiting=true;
            if(homeButton)homeButton.interactable=false;if(yesButton)yesButton.interactable=false;if(noButton)noButton.interactable=false;
            if(exitStatus)exitStatus.text=savingMessage;
            StartCoroutine(SaveAndReturn(scene));
        }
        IEnumerator SaveAndReturn(string scene){
            Task<string> saving=null;Module4Session session4=null;
            if(module1){
                // The existing OnDisable performs gaze, answer, perspective and VO cleanup.
                module1.StopAllCoroutines();module1.enabled=false;
                if(module1.answerRecorder&&module1.answerRecorder.IsRecording)module1.answerRecorder.StopAndSave("Home button confirmed / left early");
                if(module1.session)module1.session.Record(module1.CurrentStep.ToString(),"ReturnedHomeEarly");
                if(module1.recording)saving=module1.recording.StopAndSaveAsync("Home button confirmed / left early");
            }else if(module4){
                session4=Module4Session.Instance;
                module4.StopAllCoroutines();module4.enabled=false;
                if(session4){
                    session4.CancelNarration();
                    var answers=session4.GetComponent<Module4AnswerRecorder>();
                    if(answers&&answers.IsRecording)answers.StopAndSave(false);
                    session4.Log("ReturnedHomeEarly");session4.Save();
                    var video=session4.GetComponent<Module4VideoRecording>();
                    if(video)saving=video.StopAndSave("Home button confirmed / left early");
                }
            }
            while(saving!=null&&!saving.IsCompleted)yield return null;
            if(saving!=null&&saving.IsFaulted)Debug.LogError("[ModuleHome] Save failed; recorder pending files are retained: "+saving.Exception,this);
            if(session4){
                // Release a preloaded Home before another load, otherwise Unity's queue can stall.
                yield return session4.DiscardPreparedPerspective();
                // A child-view visit retains Walk's event system under the persistent session.
                // Return it to scene ownership so it cannot outlive the rig it points at.
                var sharedUi=session4.SharedWalkUi;
                if(sharedUi&&sharedUi.transform.IsChildOf(session4.transform)){
                    sharedUi.transform.SetParent(null,true);
                    SceneManager.MoveGameObjectToScene(sharedUi.gameObject,gameObject.scene);
                }
            }
            Debug.Log("[ModuleHome] Recording stopped and finalized; returning to "+scene,this);
            var folders=FolderManager.Instance;
            if(folders){var returning=folders.ReturnToEntryAsync();while(!returning.IsCompleted)yield return null;
                if(returning.IsFaulted)Debug.LogError("[ModuleHome] Return failed: "+returning.Exception,this);
            }else{var load=SceneManager.LoadSceneAsync(scene,LoadSceneMode.Single);if(load!=null)while(!load.isDone)yield return null;}
        }
        void OnDestroy(){
            if(homeButton)homeButton.onClick.RemoveListener(OpenConfirmation);
            if(yesButton)yesButton.onClick.RemoveListener(ConfirmYes);
            if(noButton)noButton.onClick.RemoveListener(ConfirmNo);
        }
    }
}
