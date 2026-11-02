using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BitsPilani.Module1.Playground {
    public sealed class PlaygroundParticipantKeyboard : MonoBehaviour, IPointerClickHandler, ISelectHandler {
        public TMP_InputField input;
        public PlaygroundSessionRecording recording;
        public Transform head;
        public bool IsOpen { get; private set; }
        public Canvas KeyboardCanvas { get; private set; }
        public readonly List<Button> KeyButtons = new List<Button>();
        TMP_Text preview, hoverReadout;
        readonly List<TMP_Text> letterLabels = new List<TMP_Text>();
        bool upper = true;
        public void OnPointerClick(PointerEventData data) { Open(); }
        public void OnSelect(BaseEventData data) { Open(); }
        public void Open() {
            if (!input) input = GetComponent<TMP_InputField>();
            if (!input || !input.interactable || recording && (!recording.showVirtualKeyboard || recording.IsRecording || recording.IsSaving)) return;
            if (!KeyboardCanvas) Build();
            IsOpen = true; KeyboardCanvas.gameObject.SetActive(true);
            Transform eye = head ? head : Camera.main ? Camera.main.transform : transform;
            Vector3 forward = Vector3.ProjectOnPlane(eye.forward, Vector3.up).normalized;
            KeyboardCanvas.transform.SetPositionAndRotation(eye.position + forward * 1.25f - Vector3.up * .35f, Quaternion.LookRotation(forward));
            preview.text = string.IsNullOrEmpty(input.text) ? "Your participant ID" : input.text;
        }
        void Build() {
            var root = new GameObject("Participant ID virtual keyboard", typeof(RectTransform));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, input.gameObject.scene);
            root.layer = 5;
            KeyboardCanvas = root.AddComponent<Canvas>(); KeyboardCanvas.renderMode = RenderMode.WorldSpace; KeyboardCanvas.sortingOrder = 75;
            var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(1920, 1080); rect.localScale = Vector3.one * .00075f;
            root.AddComponent<GraphicRaycaster>(); var image = root.AddComponent<Image>(); PlaygroundUIStyle.Surface(image, PlaygroundUIStyle.Panel, true); PlaygroundUIStyle.CardShadow(image, .18f, 8f);
            var source = input.GetComponentInParent<Canvas>(); if (source) KeyboardCanvas.worldCamera = source.worldCamera;
            var rig = recording ? recording.GetComponent<PlaygroundFlowController>()?.rig : null;
            if (rig && rig.vrUi && rig.vrUi.enabled) rig.vrUi.AddCanvas(KeyboardCanvas);
            preview = Label(root.transform, "Participant ID", new Vector2(1720,100), new Vector2(0,430), 64);
            hoverReadout = Label(root.transform, "Point at a key, then release the trigger to type", new Vector2(1760,76), new Vector2(0,325), 44);
            hoverReadout.color = PlaygroundUIStyle.Mint;
            input.selectionColor = new Color(.7f,.7f,.7f,.5f);
            input.customCaretColor = true; input.caretColor = Color.black;
            if (input.textComponent) input.textComponent.color = Color.black;
            if (input.placeholder is TMP_Text placeholder) placeholder.color = Color.black;
            string[] rows = { "1234567890", "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };
            for (int row=0; row<rows.Length; row++) for (int col=0; col<rows[row].Length; col++) {
                string key = rows[row][col].ToString();
                Key(root.transform,key,new Vector2(140,110),new Vector2((col-(rows[row].Length-1)*.5f)*170,200-row*135),()=>Press(key));
            }
            Key(root.transform,"Shift",new Vector2(320,110),new Vector2(-640,-400),()=>Press("shift"));
            Key(root.transform,"Backspace",new Vector2(420,110),new Vector2(-240,-400),()=>Press("backspace"));
            Key(root.transform,"Done",new Vector2(340,110),new Vector2(190,-400),Done);
            Key(root.transform,"Close",new Vector2(320,110),new Vector2(560,-400),Close);
            input.shouldHideSoftKeyboard = true;
        }
        TMP_Text Label(Transform parent,string text,Vector2 size,Vector2 pos,float font) {
            var go=new GameObject("Label",typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.sizeDelta=size;rect.anchoredPosition=pos;
            var label=go.AddComponent<TextMeshProUGUI>();label.font=input.textComponent?input.textComponent.font:TMP_Settings.defaultFontAsset;
            label.text=text;label.fontSize=font;label.fontStyle=FontStyles.Bold;label.color=Color.black;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;return label;
        }
        void Key(Transform parent,string text,Vector2 size,Vector2 pos,UnityEngine.Events.UnityAction callback) {
            var go=new GameObject(text,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.sizeDelta=size;rect.anchoredPosition=pos;
            var image=go.AddComponent<Image>();image.color=Color.white;
            var button=go.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(callback);KeyButtons.Add(button);
            var label = Label(go.transform,text,size - new Vector2(16,10),Vector2.zero,64);
            PlaygroundUIStyle.Button(button, label, text == "Done");
            label.fontStyle=FontStyles.Bold; label.enableAutoSizing=true; label.fontSize=64; label.fontSizeMin=48; label.fontSizeMax=64;
            if (text.Length == 1 && char.IsLetter(text[0])) letterLabels.Add(label);
            button.GetComponent<PlaygroundButtonFeedback>().HoverChanged = focused => {
                if (!hoverReadout) return;
                hoverReadout.text = focused ? "SELECT KEY: " + label.text : "Point at a key, then release the trigger to type";
                hoverReadout.color = Color.black;
            };
        }
        public void Press(string key) {
            if (!IsOpen || !input || !input.interactable) return;
            string value=input.text;
            if(key=="shift") { upper=!upper;
                foreach(var label in letterLabels) label.text = upper ? label.text.ToUpperInvariant() : label.text.ToLowerInvariant();
                hoverReadout.text = upper ? "Uppercase letters" : "Lowercase letters"; return;
            }
            if(key=="backspace") { if(value.Length>0)value=value.Substring(0,value.Length-1); }
            else if(key.Length==1 && char.IsLetterOrDigit(key[0]) && value.Length<64) value+=upper?key.ToUpperInvariant():key.ToLowerInvariant();
            input.SetTextWithoutNotify(value);input.onValueChanged.Invoke(value);input.caretPosition=value.Length;input.ForceLabelUpdate();preview.text=value;
        }
        public void Done() {
            if (!input || !PlaygroundSessionRecording.IsValidParticipantId(input.text)) { if(preview)preview.text="Use 4+ characters, including a letter and a number. No spaces.";return; }
            Close(); if(recording)recording.SetParticipantId(input.text); else input.onEndEdit.Invoke(input.text);
        }
        public void Close() { IsOpen=false;if(KeyboardCanvas)KeyboardCanvas.gameObject.SetActive(false); }
        void OnDisable() { Close(); }
        void OnDestroy() { if(KeyboardCanvas) { if(Application.isPlaying)Destroy(KeyboardCanvas.gameObject); else DestroyImmediate(KeyboardCanvas.gameObject); } }
    }
}
