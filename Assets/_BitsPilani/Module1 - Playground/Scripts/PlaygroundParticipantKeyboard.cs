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
        TMP_Text preview;
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
            preview.text = input.text;
        }
        void Build() {
            var root = new GameObject("Participant ID virtual keyboard", typeof(RectTransform));
            KeyboardCanvas = root.AddComponent<Canvas>(); KeyboardCanvas.renderMode = RenderMode.WorldSpace; KeyboardCanvas.sortingOrder = 75;
            var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(900, 440); rect.localScale = Vector3.one * .001f;
            root.AddComponent<GraphicRaycaster>(); var image = root.AddComponent<Image>(); image.color = new Color(.055f,.12f,.17f,.99f);
            var source = input.GetComponentInParent<Canvas>(); if (source) KeyboardCanvas.worldCamera = source.worldCamera;
            var rig = recording ? recording.GetComponent<PlaygroundFlowController>()?.rig : null;
            if (rig && rig.vrUi && rig.vrUi.enabled) rig.vrUi.AddCanvas(KeyboardCanvas);
            preview = Label(root.transform, "Participant ID", new Vector2(820,42), new Vector2(0,180), 28);
            string[] rows = { "1234567890", "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };
            for (int row=0; row<rows.Length; row++) for (int col=0; col<rows[row].Length; col++) {
                string key = rows[row][col].ToString();
                Key(root.transform,key,new Vector2(66,53),new Vector2((col-(rows[row].Length-1)*.5f)*76,115-row*63),()=>Press(key));
            }
            Key(root.transform,"Shift",new Vector2(150,53),new Vector2(-300,-155),()=>Press("shift"));
            Key(root.transform,"Backspace",new Vector2(200,53),new Vector2(-110,-155),()=>Press("backspace"));
            Key(root.transform,"Done",new Vector2(180,53),new Vector2(110,-155),Done);
            Key(root.transform,"Close",new Vector2(150,53),new Vector2(300,-155),Close);
            input.shouldHideSoftKeyboard = true;
        }
        TMP_Text Label(Transform parent,string text,Vector2 size,Vector2 pos,float font) {
            var go=new GameObject("Label",typeof(RectTransform));go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.sizeDelta=size;rect.anchoredPosition=pos;
            var label=go.AddComponent<TextMeshProUGUI>();label.font=input.textComponent?input.textComponent.font:TMP_Settings.defaultFontAsset;
            label.text=text;label.fontSize=font;label.color=Color.white;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;return label;
        }
        void Key(Transform parent,string text,Vector2 size,Vector2 pos,UnityEngine.Events.UnityAction callback) {
            var go=new GameObject(text,typeof(RectTransform));go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.sizeDelta=size;rect.anchoredPosition=pos;
            var image=go.AddComponent<Image>();image.color=new Color(.12f,.30f,.34f);
            var button=go.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(callback);KeyButtons.Add(button);
            Label(go.transform,text,size,Vector2.zero,24);
        }
        public void Press(string key) {
            if (!IsOpen || !input || !input.interactable) return;
            string value=input.text;
            if(key=="shift") { upper=!upper; return; }
            if(key=="backspace") { if(value.Length>0)value=value.Substring(0,value.Length-1); }
            else if(key.Length==1 && char.IsLetterOrDigit(key[0]) && value.Length<64) value+=upper?key.ToUpperInvariant():key.ToLowerInvariant();
            input.SetTextWithoutNotify(value);input.onValueChanged.Invoke(value);input.caretPosition=value.Length;input.ForceLabelUpdate();preview.text=value;
        }
        public void Done() {
            if (!input || !PlaygroundSessionRecording.IsValidParticipantId(input.text)) { if(preview)preview.text="Enter letters and numbers only.";return; }
            Close(); if(recording)recording.SetParticipantId(input.text); else input.onEndEdit.Invoke(input.text);
        }
        public void Close() { IsOpen=false;if(KeyboardCanvas)KeyboardCanvas.gameObject.SetActive(false); }
        void OnDisable() { Close(); }
        void OnDestroy() { if(KeyboardCanvas)Destroy(KeyboardCanvas.gameObject); }
    }
}
