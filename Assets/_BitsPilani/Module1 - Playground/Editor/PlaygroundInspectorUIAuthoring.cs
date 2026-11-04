#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BitsPilani.Module1.Playground;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UI.ProceduralImage;
using Object=UnityEngine.Object;

public static class PlaygroundInspectorUIAuthoring {
    const string Module="Assets/_BitsPilani/Module1 - Playground/";
    [Serializable] class Defaults { public PlaygroundUITextEntry[] entries; }
    class Reference { public Component owner; public string path; public int oldId; }
    static T[] All<T>(Scene scene)where T:Component=>scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).ToArray();
    static RectTransform Rect(Transform t)=>t as RectTransform;
    static void Place(Transform t,float width,float height,float x,float y){var r=Rect(t);r.anchorMin=r.anchorMax=r.pivot=Vector2.one*.5f;r.sizeDelta=new Vector2(width,height);r.anchoredPosition=new Vector2(x,y);}
    static GameObject Node(string name,Transform parent){var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);Undo.RegisterCreatedObjectUndo(go,"Author editable UI");return go;}
    static ProceduralImage Surface(string name,Transform parent,float w,float h,float x,float y,Color color,float radius=24){
        var go=Node(name,parent);Place(go.transform,w,h,x,y);var image=Undo.AddComponent<ProceduralImage>(go);image.ModifierType=typeof(UniformModifier);go.GetComponent<UniformModifier>().Radius=radius;image.color=color;return image;
    }
    static TMP_Text Label(string name,Transform parent,TMP_FontAsset font,string text,float w,float h,float x,float y,float size){
        var go=Node(name,parent);Place(go.transform,w,h,x,y);var label=Undo.AddComponent<TextMeshProUGUI>(go);label.font=font;label.text=text;label.color=Color.black;label.fontStyle=FontStyles.Bold;label.fontSize=size;label.enableAutoSizing=true;label.fontSizeMin=size-8;label.fontSizeMax=size;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;return label;
    }
    static Button Key(string name,string caption,Transform parent,TMP_FontAsset font,float w,float h,float x,float y){
        var image=Surface(name,parent,w,h,x,y,Color.white);var button=Undo.AddComponent<Button>(image.gameObject);button.targetGraphic=image;button.transition=Selectable.Transition.None;var label=Label("Label",button.transform,font,caption,w-16,h-10,0,0,64);
        Undo.AddComponent<PlaygroundButtonFeedback>(button.gameObject);return button;
    }
    static bool SceneOwned(Component component)=>!PrefabUtility.IsPartOfPrefabInstance(component)||PrefabUtility.IsAddedGameObjectOverride(component.gameObject);
    static void ConvertImages(Scene scene){
        var images=All<Image>(scene).Where(i=>i.GetType()==typeof(Image)&&SceneOwned(i)&&i.GetComponentInParent<Canvas>()).ToArray();
        var ids=new HashSet<int>(images.Select(i=>i.GetInstanceID()));var references=new List<Reference>();
        foreach(var owner in All<Component>(scene).Where(c=>c&&c is MonoBehaviour)){
            var serialized=new SerializedObject(owner);var property=serialized.GetIterator();
            while(property.Next(true))if(property.propertyType==SerializedPropertyType.ObjectReference&&ids.Contains(property.objectReferenceInstanceIDValue))references.Add(new Reference{owner=owner,path=property.propertyPath,oldId=property.objectReferenceInstanceIDValue});
        }
        var replacements=new Dictionary<int,ProceduralImage>();
        foreach(var old in images){
            int id=old.GetInstanceID();var go=old.gameObject;var color=old.color;bool ray=old.raycastTarget,mask=old.maskable,enabled=old.enabled;var padding=old.raycastPadding;var sprite=old.sprite;
            var type=old.type;var fill=old.fillAmount;var method=old.fillMethod;var origin=old.fillOrigin;var clockwise=old.fillClockwise;
            bool round=sprite&&sprite.name=="Module rounded panel";
            Undo.DestroyObjectImmediate(old);var image=Undo.AddComponent<ProceduralImage>(go);image.color=color;image.raycastTarget=ray;image.raycastPadding=padding;image.maskable=mask;
            image.ModifierType=typeof(UniformModifier);go.GetComponent<UniformModifier>().Radius=round?24:0;
            if(sprite&&!round)image.sprite=sprite;
            image.type=type==Image.Type.Filled?Image.Type.Filled:Image.Type.Simple;image.fillAmount=fill;image.fillMethod=method;image.fillOrigin=origin;image.fillClockwise=clockwise;image.enabled=enabled;replacements[id]=image;
        }
        foreach(var reference in references)if(reference.owner){var serialized=new SerializedObject(reference.owner);var property=serialized.FindProperty(reference.path);if(property!=null){property.objectReferenceValue=replacements[reference.oldId];serialized.ApplyModifiedProperties();}}
        Debug.Log("[UI Authoring] Converted "+images.Length+" remaining Images; preserved existing Procedural Images, colors and scene references.");
    }
    static PlaygroundUITextCatalog Catalog(GameObject target,string prefix){
        var asset=AssetDatabase.LoadAssetAtPath<TextAsset>(Module+"UI/InspectorTextDefaults.json");var defaults=JsonUtility.FromJson<Defaults>(asset.text);
        var existing=target.GetComponent<PlaygroundUITextCatalog>();
        if(existing){var known=new HashSet<string>((existing.entries??new PlaygroundUITextEntry[0]).Where(e=>e!=null).Select(e=>e.id));existing.entries=(existing.entries??new PlaygroundUITextEntry[0]).Concat(defaults.entries.Where(e=>!known.Contains(e.id)&&(prefix=="main"?!e.id.StartsWith("entry_"):e.id.StartsWith("entry_")))).ToArray();EditorUtility.SetDirty(existing);return existing;}
        var catalog=Undo.AddComponent<PlaygroundUITextCatalog>(target);catalog.entries=defaults.entries.Where(e=>prefix=="main"?!e.id.StartsWith("entry_"):e.id.StartsWith("entry_")).ToArray();return catalog;
    }
    static void AuthorKeyboard(PlaygroundFlowController flow){
        var recording=flow.recording;var input=recording.participantIdInput;var keyboard=input.GetComponent<PlaygroundParticipantKeyboard>();if(!keyboard)keyboard=Undo.AddComponent<PlaygroundParticipantKeyboard>(input.gameObject);
        keyboard.input=input;keyboard.recording=recording;keyboard.head=flow.rig.head;
        if(!keyboard.keyboardCanvas){
            var root=Node("Participant ID keyboard - editable",recording.participantIdPanel.transform);var canvas=Undo.AddComponent<Canvas>(root);canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=75;canvas.worldCamera=input.GetComponentInParent<Canvas>().worldCamera;
            Rect(root.transform).sizeDelta=new Vector2(1920,1080);root.transform.localScale=Vector3.one*.75f;root.transform.localRotation=Quaternion.identity;root.transform.localPosition=new Vector3(0,-995,0);Undo.AddComponent<GraphicRaycaster>(root);
            var background=Undo.AddComponent<ProceduralImage>(root);background.ModifierType=typeof(UniformModifier);root.GetComponent<UniformModifier>().Radius=24;background.color=new Color(.955f,.955f,.955f);
            keyboard.keyboardCanvas=canvas;keyboard.preview=Label("Typed participant ID",root.transform,input.textComponent.font,keyboard.emptyPreviewText,1720,110,0,400,64);
            var slots=new List<PlaygroundKeyboardKey>();string[] rows={"1234567890","QWERTYUIOP","ASDFGHJKL","ZXCVBNM"};
            for(int row=0;row<rows.Length;row++)for(int col=0;col<rows[row].Length;col++){
                string character=rows[row][col].ToString();var b=Key("Key "+character,character,root.transform,input.textComponent.font,140,110,(col-(rows[row].Length-1)*.5f)*170,200-row*135);
                slots.Add(new PlaygroundKeyboardKey{key=character,button=b,label=b.GetComponentInChildren<TMP_Text>(true)});
            }
            string[] actions={"shift","backspace","done","close"};string[] captions={"Shift","Backspace","Done","Close"};float[] widths={320,420,340,320};float[] xs={-640,-240,190,560};
            for(int i=0;i<4;i++){var b=Key(captions[i],captions[i],root.transform,input.textComponent.font,widths[i],110,xs[i],-400);slots.Add(new PlaygroundKeyboardKey{key=actions[i],button=b,label=b.GetComponentInChildren<TMP_Text>(true)});if(i==2){keyboard.doneButton=b;b.interactable=false;}}
            keyboard.keys=slots.ToArray();root.SetActive(false);
        }
        var panel=recording.participantIdPanel.transform;panel.SetPositionAndRotation(flow.ui.mainCanvas.transform.position,flow.ui.mainCanvas.transform.rotation);
        foreach(var text in panel.GetComponentsInChildren<TMP_Text>(true)){
            if(text.text.Contains("at least four")||text.text.Contains("at least 4"))text.text="Use at least 3 letters or numbers. Numbers only, letters only, or both. No spaces.";
            if(text.text.Contains("4+ letters/numbers"))text.text="Participant ID (3+ letters or numbers)";
        }
        panel.gameObject.SetActive(true);flow.ui.mainCanvas.gameObject.SetActive(false);EditorUtility.SetDirty(keyboard);
        flow.rig.moduleCanvases=flow.rig.moduleCanvases.Concat(new[]{keyboard.keyboardCanvas,input.GetComponentInParent<Canvas>()}).Where(c=>c).Distinct().ToArray();EditorUtility.SetDirty(flow.rig);
    }
    static void AuthorScrollbar(PlaygroundModuleUI ui){
        var scroll=ui.narrativeScroll;if(!scroll)return;
        if(!ui.narrativeScrollbar){
            var track=Surface("Narrative scrollbar - editable",scroll.transform,38,480,0,0,new Color(.87f,.87f,.87f),16);var r=Rect(track.transform);r.anchorMin=new Vector2(1,0);r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,.5f);r.sizeDelta=new Vector2(38,-20);r.anchoredPosition=new Vector2(-24,0);
            var bar=Undo.AddComponent<Scrollbar>(track.gameObject);var area=Node("Sliding area",track.transform);var ar=Rect(area.transform);ar.anchorMin=Vector2.zero;ar.anchorMax=Vector2.one;ar.sizeDelta=new Vector2(-8,-8);ar.anchoredPosition=Vector2.zero;
            var handle=Surface("Handle",area.transform,30,80,0,0,Color.black,12);bar.handleRect=Rect(handle.transform);bar.handleRect.sizeDelta=Vector2.zero;bar.handleRect.anchoredPosition=Vector2.zero;bar.targetGraphic=handle;bar.direction=Scrollbar.Direction.BottomToTop;bar.transition=Selectable.Transition.None;
            ui.narrativeScrollbar=bar;scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;track.gameObject.SetActive(false);
        }
        scroll.viewport.offsetMin=new Vector2(10,10);scroll.viewport.offsetMax=new Vector2(-66,-10);
        if(ui.narrativeUp)ui.narrativeUp.gameObject.SetActive(false);if(ui.narrativeDown)ui.narrativeDown.gameObject.SetActive(false);
    }
    static PlaygroundUIRayTurnLock TurnLock(Scene scene,GameObject target,BNG.VRUISystem vrUi){
        var locker=target.GetComponent<PlaygroundUIRayTurnLock>();if(!locker)locker=Undo.AddComponent<PlaygroundUIRayTurnLock>(target);
        locker.vrUi=vrUi;locker.rotationDrivers=All<BNG.PlayerRotation>(scene);EditorUtility.SetDirty(locker);return locker;
    }
    static void AuthorTouches(Scene scene,PlaygroundFlowController flow){
        foreach(var side in new[]{BNG.ControllerHand.Left,BNG.ControllerHand.Right}){
            string name="Child touch haptics - "+side;
            var existing=All<PlaygroundChildTouchHaptics>(scene).FirstOrDefault(h=>h.hand==side&&h.childRoot==flow.child.transform);if(existing)continue;
            var grabber=flow.rig.playerRoot.GetComponentsInChildren<BNG.Grabber>(true).FirstOrDefault(g=>g.HandSide==side);
            Transform hand=grabber?grabber.transform:side==BNG.ControllerHand.Left?flow.ui.leftHand:flow.rig.controllerPointers.First(p=>p.ControllerSide==side).transform;
            var sensor=new GameObject(name);Undo.RegisterCreatedObjectUndo(sensor,"Add child touch feedback");sensor.layer=2;sensor.transform.SetParent(hand,false);
            var collider=Undo.AddComponent<SphereCollider>(sensor);collider.isTrigger=true;collider.radius=.08f;var body=Undo.AddComponent<Rigidbody>(sensor);body.isKinematic=true;body.useGravity=false;
            var haptic=Undo.AddComponent<PlaygroundChildTouchHaptics>(sensor);haptic.childRoot=flow.child.transform;haptic.hand=side;haptic.rig=flow.rig;haptic.inputBridge=All<BNG.InputBridge>(scene).FirstOrDefault();
        }
    }
    [MenuItem("Tools/Bits Pilani/UI/Prepare inspector-authored procedural UI")]
    public static void Prepare(){
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode first.");
        var original=SceneManager.GetActiveScene();
        foreach(var path in new[]{Module+"Scenes/Module1_Playground.unity","Assets/_BitsPilani/Module Selection/Scenes/Autism_ModuleEntry.unity"}){
            var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try{
                // Keep a snapshot of user-authored colors and procedural modifiers before touching component types.
                var colors=All<Image>(scene).Where(SceneOwned).ToDictionary(i=>i.gameObject,i=>i.color);
                var procedural=All<ProceduralImage>(scene).ToDictionary(i=>i, i=>i.GetComponent<ProceduralImageModifier>()?EditorJsonUtility.ToJson(i.GetComponent<ProceduralImageModifier>()):"");
                ConvertImages(scene);
                var flow=All<PlaygroundFlowController>(scene).FirstOrDefault();
                if(flow){
                    Undo.RecordObject(flow.ui,"Configure authored UI behaviour");flow.ui.textCatalog=Catalog(flow.ui.gameObject,"main");AuthorKeyboard(flow);AuthorScrollbar(flow.ui);
                    flow.rig.uiTurnLock=TurnLock(scene,flow.rig.gameObject,flow.rig.vrUi);AuthorTouches(scene,flow);
                    var close=flow.ui.thoughtClose.targetGraphic;close.color=Color.black;foreach(var t in flow.ui.thoughtClose.GetComponentsInChildren<TMP_Text>(true))t.color=Color.white;
                    flow.ui.thoughtText.fontSize=Mathf.Max(96,flow.ui.thoughtText.fontSize);flow.ui.thoughtText.enableAutoSizing=true;flow.ui.thoughtText.fontSizeMin=80;flow.ui.thoughtText.fontSizeMax=Mathf.Max(96,flow.ui.thoughtText.fontSizeMax);
                    flow.ui.clueTool.interactable=false;flow.ui.guessTool.interactable=false;flow.ui.wristHelp.interactable=false;
                    EditorUtility.SetDirty(flow.ui);EditorUtility.SetDirty(flow);EditorUtility.SetDirty(flow.rig);
                }
                var entry=All<ModuleEntryController>(scene).FirstOrDefault();if(entry){entry.textCatalog=Catalog(entry.gameObject,"entry");TurnLock(scene,entry.gameObject,entry.vrUi);EditorUtility.SetDirty(entry);}
                foreach(var pair in colors){var graphic=pair.Key?pair.Key.GetComponent<Image>():null;if(!graphic)throw new InvalidOperationException("A UI graphic was lost.");if(flow&&graphic==flow.ui.thoughtClose.targetGraphic)continue;if(graphic.color!=pair.Value)throw new InvalidOperationException("User color changed: "+pair.Key.name);}
                foreach(var pair in procedural)if(pair.Key&&EditorJsonUtility.ToJson(pair.Key.GetComponent<ProceduralImageModifier>())!=pair.Value)throw new InvalidOperationException("An existing procedural modifier changed.");
                EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene could not be saved: "+path);
            }finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
        if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);
        Debug.Log("[UI Authoring] Saved procedural UI, editable keyboard/text catalogs, conditional scrollbar, ray turn lock and child-touch haptics. Existing user colors and procedural modifiers preserved.");
    }
    public static void SyncTextCatalogs(){
        var original=SceneManager.GetActiveScene();
        foreach(var path in new[]{Module+"Scenes/Module1_Playground.unity","Assets/_BitsPilani/Module Selection/Scenes/Autism_ModuleEntry.unity"}){
            var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try{var ui=All<PlaygroundModuleUI>(scene).FirstOrDefault();if(ui){ui.textCatalog=Catalog(ui.gameObject,"main");if(ui.narrativeScrollbar&&ui.narrativeScrollbar.handleRect.sizeDelta==new Vector2(30,80))ui.narrativeScrollbar.handleRect.sizeDelta=Vector2.zero;EditorUtility.SetDirty(ui);}var entry=All<ModuleEntryController>(scene).FirstOrDefault();if(entry){entry.textCatalog=Catalog(entry.gameObject,"entry");EditorUtility.SetDirty(entry);}EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
        if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);
    }
}
#endif
