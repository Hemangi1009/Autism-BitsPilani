using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BitsPilani.Module1.Playground {
    public sealed class ModuleEntryController : MonoBehaviour {
        public PlaygroundUITextCatalog textCatalog;
        public string Copy(string id) => textCatalog ? textCatalog.Get(id) : id;
        public FolderManager folderManager;
        public Button[] moduleButtons;
        public TMP_Text status;
        public BNG.VRUISystem vrUi;
        public BNG.UIPointer rightPointer;
        public Canvas menuCanvas;
        [Tooltip("Preserve the saved canvas appearance and layout when entering Play mode.")] public bool useAuthoredCanvas;
        [HideInInspector] public Button administratorSetupButton;
        [Header("Menu comfort")]
        [Tooltip("Disabled by default: the entry menu stays at its authored world position.")] public bool followPlayer = false;
        [Min(1.5f)] public float menuDistance = 2.1f;
        [Range(.1f, 1)] public float menuFollowSmoothTime = .4f;
        bool styled, menuPlaced;
        Transform menuHead;
        Vector3 menuForward, menuVelocity;
        PlaygroundPanelHover menuHover;
        bool storageReady, opening;
        float nextCheck;

        void Start() {
            ApplyVisualStyle();
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

        public void ApplyVisualStyle() {
            if (styled || !menuCanvas || moduleButtons == null || moduleButtons.Length != 4) return;
            styled = true;
            if (useAuthoredCanvas || Application.isPlaying) { BindMenuPose(); return; }
            var root = (RectTransform)menuCanvas.transform;
            root.sizeDelta = new Vector2(1280, 1040); root.localScale = Vector3.one * .00165f;
            PlaygroundUIStyle.Surface(menuCanvas.GetComponent<Image>(), PlaygroundUIStyle.Panel, true);
            PlaygroundUIStyle.CardShadow(menuCanvas.GetComponent<Image>(), .12f, 6f);
            var heading = root.Find("Title")?.GetComponent<TMP_Text>();
            var subtitle = root.Find("Subtitle")?.GetComponent<TMP_Text>();
            var font = heading ? heading.font : TMP_Settings.defaultFontAsset;
            if (heading) { heading.text = Copy("entry_4442e4e8d6"); Place(heading.rectTransform, new Vector2(1160, 92), new Vector2(0, 350)); heading.fontSize = 48; heading.fontStyle = FontStyles.Bold; heading.color = PlaygroundUIStyle.Text; heading.enableAutoSizing = true; heading.fontSizeMin = 36; heading.fontSizeMax = 48; }
            if (subtitle) { subtitle.text = Copy("entry_96e68cabbf"); Place(subtitle.rectTransform, new Vector2(1120, 52), new Vector2(0, 272)); subtitle.fontSize = 27; subtitle.color = PlaygroundUIStyle.Muted; }
            var brand = PlaygroundUIStyle.Label("Module hub brand", root, font, "BITS PILANI   /   VR TRAINING", new Vector2(1050, 44), new Vector2(0, 442), 23);
            brand.color = PlaygroundUIStyle.Mint; brand.characterSpacing = 3;
            for (int i = 0; i < moduleButtons.Length; i++) {
                var button = moduleButtons[i]; if (!button) continue;
                Place((RectTransform)button.transform, new Vector2(548, 232), new Vector2(i % 2 == 0 ? -292 : 292, i < 2 ? 92 : -176));
                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label) { label.text = i == 0 ? Copy("entry_a68eb23082") : Copy("entry_e4115be258"); Place(label.rectTransform, new Vector2(482, 64), new Vector2(0, 7)); label.fontStyle = FontStyles.Bold; }
                PlaygroundUIStyle.Button(button, label, i == 0);
                if (label) { label.fontSizeMin = 30; label.fontSizeMax = 38; }
                var number = PlaygroundUIStyle.Label("Module number", button.transform, font, "MODULE " + (i + 1).ToString("00"), new Vector2(482, 36), new Vector2(0, 76), 22);
                var detail = PlaygroundUIStyle.Label("Module description", button.transform, font, i == 0 ? "Too loud on the slide\nExplore, notice and reflect" : "This module is being prepared", new Vector2(478, 68), new Vector2(0, -66), 23);
                number.color = PlaygroundUIStyle.Muted; detail.color = i == 0 ? PlaygroundUIStyle.Text : PlaygroundUIStyle.Muted;
                if (label && i != 0) label.color = PlaygroundUIStyle.Muted;
            }
            if (status) {
                Place(status.rectTransform, new Vector2(1090, 88), new Vector2(0, -370)); status.color = PlaygroundUIStyle.Mint;
                status.enableAutoSizing = true; status.fontSizeMin = 20; status.fontSizeMax = 25;
                var statusRoot = new GameObject("Session message inset", typeof(RectTransform)); statusRoot.layer = 5; statusRoot.transform.SetParent(root, false);
                Place((RectTransform)statusRoot.transform, new Vector2(1140, 108), new Vector2(0, -370));
                var image = statusRoot.AddComponent<Image>(); PlaygroundUIStyle.Surface(image, PlaygroundUIStyle.Inset); PlaygroundUIStyle.CardShadow(image, .08f, 3f); image.raycastTarget = false;
                statusRoot.transform.SetSiblingIndex(status.transform.GetSiblingIndex());
            }
            var help = PlaygroundUIStyle.Label("Selection help", root, font, "Right controller: point and release the trigger to select", new Vector2(1140, 34), new Vector2(0, -470), 21);
            help.color = PlaygroundUIStyle.Muted;
            foreach (var text in menuCanvas.GetComponentsInChildren<TMP_Text>(true)) text.raycastTarget = false;
            menuHover = menuCanvas.GetComponent<PlaygroundPanelHover>(); if (!menuHover) menuHover = menuCanvas.gameObject.AddComponent<PlaygroundPanelHover>();
            BindMenuPose();
        }
        public void BakeCanvasForEditing() { if (useAuthoredCanvas) return; ApplyVisualStyle(); useAuthoredCanvas = true; }
        void BindMenuPose() {
            menuHover = menuCanvas ? menuCanvas.GetComponent<PlaygroundPanelHover>() : null;
            if (rightPointer) { var player = rightPointer.GetComponentInParent<BNG.BNGPlayerController>(); if (player) menuHead = player.CenterEyeAnchor; }
        }
        static void Place(RectTransform rect, Vector2 size, Vector2 position) { rect.sizeDelta = size; rect.anchoredPosition = position; }
        void LateUpdate() {
            if (!followPlayer || !menuHead || !menuCanvas) return;
            Vector3 look = Vector3.ProjectOnPlane(menuHead.forward, Vector3.up).normalized;
            if (look.sqrMagnitude < .1f) return;
            if (!menuPlaced || (!menuHover || !menuHover.IsHovered) && Vector3.Angle(menuForward, look) > 75) menuForward = look;
            Vector3 target = menuHead.position + menuForward * Mathf.Max(1.5f, menuDistance) - Vector3.up * .1f;
            if (!menuPlaced) { menuCanvas.transform.SetPositionAndRotation(target, Quaternion.LookRotation(menuForward)); menuPlaced = true; }
            menuCanvas.transform.position = Vector3.SmoothDamp(menuCanvas.transform.position, target, ref menuVelocity, menuFollowSmoothTime, 3f, Time.unscaledDeltaTime);
            Vector3 away = menuCanvas.transform.position - menuHead.position;
            if (away.sqrMagnitude > .001f) menuCanvas.transform.rotation = Quaternion.Slerp(menuCanvas.transform.rotation, Quaternion.LookRotation(away, Vector3.up), 1 - Mathf.Exp(-Time.unscaledDeltaTime / menuFollowSmoothTime));
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
            if(storageReady && status) status.text=Copy("entry_1496b242d5") + (string.IsNullOrEmpty(folderManager.LastSessionMessage)?"":"\n"+folderManager.LastSessionMessage);
        }
        void ApplyModuleAvailability() { for(int i=0;i<moduleButtons.Length;i++)moduleButtons[i].interactable=!opening&&storageReady&&!folderManager.IsLoading&&folderManager.IsAvailable(i+1); }
        public void OpenAdministratorSetup() { /* Kept for existing scene references. No setup or permission UI. */ }
        public void ChooseModule(int module) {
            if (!folderManager || !storageReady || opening || folderManager.IsLoading || !folderManager.IsAvailable(module)) return;
            opening=true;ApplyModuleAvailability();
            if(status)status.text=Copy("entry_df7ff434e4") + module + "...";
            folderManager.OpenModule(module);
        }
    }
}
