using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BitsPilani.Module1.Playground {
    public sealed class ModuleEntryController : MonoBehaviour {
        public FolderManager folderManager;
        public Button[] moduleButtons;
        public TMP_Text status;
        public BNG.VRUISystem vrUi;
        public BNG.UIPointer rightPointer;
        public Canvas menuCanvas;
        [HideInInspector] public Button administratorSetupButton;
        bool storageReady, opening;
        float nextCheck;

        void Start() {
            folderManager = FolderManager.Instance ? FolderManager.Instance : folderManager;
            foreach (var pointer in Object.FindObjectsByType<global::Pointer>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                foreach (var graphic in pointer.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
                foreach (var raycaster in pointer.GetComponentsInChildren<GraphicRaycaster>(true)) raycaster.enabled = false;
            }
            if (vrUi && rightPointer) {
                rightPointer.gameObject.SetActive(true); rightPointer.enabled = true;
                vrUi.RightPointerTransform = rightPointer.transform; vrUi.SelectedHand = BNG.ControllerHand.Right;
                vrUi.ControllerInput = new System.Collections.Generic.List<BNG.ControllerBinding> { BNG.ControllerBinding.RightTrigger };
                vrUi.UpdateControllerHand(BNG.ControllerHand.Right); vrUi.AddCanvas(menuCanvas);
            }
            for (int i = 0; i < moduleButtons.Length; i++) {
                int module = i + 1;
                moduleButtons[i].onClick.RemoveAllListeners();
                moduleButtons[i].onClick.AddListener(() => ChooseModule(module));
            }
            if (administratorSetupButton) administratorSetupButton.gameObject.SetActive(false);
            storageReady = folderManager;
            RefreshStorageState();
        }

        public void EnsureSetupButton() { if (administratorSetupButton) administratorSetupButton.gameObject.SetActive(false); }

        void Update() {
            if (Time.realtimeSinceStartup < nextCheck) return;
            if (opening) { if (folderManager && folderManager.IsLoading) return; opening=false; }
            nextCheck = Time.realtimeSinceStartup + .5f;
            RefreshStorageState();
        }
        void RefreshStorageState() {
            if (!folderManager || opening) return;
            storageReady=true;
            if(administratorSetupButton) administratorSetupButton.gameObject.SetActive(false);
            ApplyModuleAvailability();
            if(storageReady && status) status.text="Choose Module 1 to begin. Modules 2, 3 and 4 are coming soon." + (string.IsNullOrEmpty(folderManager.LastSessionMessage)?"":"\n"+folderManager.LastSessionMessage);
        }
        void ApplyModuleAvailability() { for(int i=0;i<moduleButtons.Length;i++)moduleButtons[i].interactable=!opening&&storageReady&&!folderManager.IsLoading&&folderManager.IsAvailable(i+1); }
        public void OpenAdministratorSetup() { /* Kept for existing scene references. No setup or permission UI. */ }
        public void ChooseModule(int module) {
            if (!folderManager || !storageReady || opening || folderManager.IsLoading || !folderManager.IsAvailable(module)) return;
            opening=true;ApplyModuleAvailability();
            if(status)status.text="Opening Module " + module + "...";
            folderManager.OpenModule(module);
        }
    }
}
