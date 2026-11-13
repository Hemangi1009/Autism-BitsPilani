using System;using TMPro;using UnityEngine;using UnityEngine.EventSystems;using UnityEngine.UI;
namespace BitsPilani.Module4 {
    [Serializable] public sealed class Module4KeyboardKey {public string key;public Button button;public TMP_Text label;}
    public sealed class Module4ParticipantKeyboard : MonoBehaviour,IPointerClickHandler,ISelectHandler {
        public Module4SceneController owner;public TMP_InputField input;
        public Canvas keyboardCanvas;public TMP_Text preview;public Button done;
        public Module4KeyboardKey[] keys;
        public bool IsOpen {get;private set;}bool bound,upper=true;
        public void Bind(){if(bound)return;bound=true;foreach(var entry in keys){var key=entry.key;entry.button.onClick.RemoveAllListeners();entry.button.onClick.AddListener(()=>Press(key));}if(input)input.onValueChanged.AddListener(_=>Refresh());Refresh();}
        public void Open(){if(!input||!input.interactable)return;Bind();IsOpen=true;keyboardCanvas.gameObject.SetActive(true);input.shouldHideSoftKeyboard=true;Refresh();}
        public void Close(){IsOpen=false;if(keyboardCanvas)keyboardCanvas.gameObject.SetActive(false);}
        public void OnPointerClick(PointerEventData e){Open();}
        public void OnSelect(BaseEventData e){Open();}
        public void Press(string key){
            if(key=="done"){if(Module4Session.IsValidId(input.text)){Close();owner.AcceptParticipantId(input.text);}return;}
            if(key=="close"){Close();return;}
            if(key=="shift"){upper=!upper;foreach(var k in keys)if(k.key.Length==1&&char.IsLetter(k.key[0]))k.label.text=upper?k.key.ToUpperInvariant():k.key.ToLowerInvariant();return;}
            string text=input.text;
            if(key=="backspace"){if(text.Length>0)text=text.Substring(0,text.Length-1);}
            else if(key.Length==1&&((key[0]>='a'&&key[0]<='z')||(key[0]>='A'&&key[0]<='Z')||char.IsDigit(key[0]))&&text.Length<64)text+=upper?key.ToUpperInvariant():key.ToLowerInvariant();
            input.text=text;input.caretPosition=text.Length;Refresh();
        }
        public void Refresh(){if(done)done.interactable=input&&Module4Session.IsValidId(input.text);if(preview)preview.text=input&&!string.IsNullOrEmpty(input.text)?input.text:"At least 3 letters or numbers. No spaces or symbols.";}
        void OnDisable(){Close();}
    }
}

