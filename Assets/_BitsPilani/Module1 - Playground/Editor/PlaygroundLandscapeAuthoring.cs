#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using BitsPilani.Module1.Playground;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class PlaygroundLandscapeAuthoring {
    const string Module="Assets/_BitsPilani/Module1 - Playground/";
    static readonly Vector2 Resolution=new Vector2(1920,1080);
    static T Find<T>(Scene s)where T:Component=>s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).FirstOrDefault();
    static RectTransform Rect(Transform t)=>t as RectTransform;
    static void Place(Transform t,Vector2 size,Vector2 position){var r=Rect(t);r.anchorMin=r.anchorMax=r.pivot=Vector2.one*.5f;r.sizeDelta=size;r.anchoredPosition=position;}
    static void Place(Transform t,float w,float h,float x,float y)=>Place(t,new Vector2(w,h),new Vector2(x,y));
    static void Text(TMP_Text t,float size,float min=0){
        if(!t)return;t.fontStyle=FontStyles.Bold;t.fontSize=size;t.enableAutoSizing=min>0;t.fontSizeMin=min>0?min:size;t.fontSizeMax=size;
        t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Overflow;t.raycastTarget=false;
        t.margin=new Vector4(8,4,8,4);
    }
    static void Button(Button b,float w,float h,float x,float y,float font=40){
        Place(b.transform,w,h,x,y);var label=b.GetComponentInChildren<TMP_Text>(true);Place(label.transform,w-32,h-16,0,0);Text(label,font,font-4);
    }
    static Image Surface(string name,Transform parent,float w,float h,float x,float y){
        var found=parent.Find(name);if(found)return found.GetComponent<Image>();
        var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.layer=5;go.transform.SetParent(parent,false);Place(go.transform,w,h,x,y);
        var image=go.GetComponent<Image>();PlaygroundUIStyle.Surface(image,Color.white,true);PlaygroundUIStyle.CardShadow(image);return image;
    }
    static Button NewButton(string name,Transform parent,string caption,float w,float h,float x,float y,float font){
        var image=Surface(name,parent,w,h,x,y);image.raycastTarget=true;var b=image.GetComponent<Button>();if(!b)b=image.gameObject.AddComponent<Button>();b.targetGraphic=image;
        var label=b.GetComponentInChildren<TMP_Text>(true);if(!label)label=PlaygroundUIStyle.Label("Label",b.transform,null,caption,new Vector2(w-32,h-16),Vector2.zero,font);
        label.text=caption;PlaygroundUIStyle.Button(b,label);Button(b,w,h,x,y,font);return b;
    }
    static TMP_Text Label(string name,Transform parent,string caption,float w,float h,float x,float y,float font){
        var found=parent.Find(name);var label=found?found.GetComponent<TMP_Text>():null;
        if(!label)label=PlaygroundUIStyle.Label(name,parent,null,caption,new Vector2(w,h),new Vector2(x,y),font);
        Place(label.transform,w,h,x,y);Text(label,font);return label;
    }
    static void ConfigureCanvas(Canvas c,float scale){Rect(c.transform).sizeDelta=Resolution;c.transform.localScale=Vector3.one*scale;var scaler=c.GetComponent<CanvasScaler>();if(scaler){scaler.referenceResolution=Resolution;EditorUtility.SetDirty(scaler);}}
    [MenuItem("Tools/Bits Pilani/UI/Apply 1920 x 1080 landscape layout")]
    public static void Apply(){
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode first.");
        PlaygroundCanvasAuthoring.Prepare();
        var sprite=AssetDatabase.LoadAllAssetsAtPath(Module+"UI/ModuleUIRounded.asset").OfType<Sprite>().FirstOrDefault();if(sprite)PlaygroundUIStyle.UseRoundedAsset(sprite);
        var original=SceneManager.GetActiveScene();
        foreach(string path in new[]{Module+"Scenes/Module1_Playground.unity","Assets/_BitsPilani/Module Selection/Scenes/Autism_ModuleEntry.unity"}){
            var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try{
                var ui=Find<PlaygroundModuleUI>(scene);if(ui)ModuleLayout(ui,Find<PlaygroundSessionRecording>(scene));
                var entry=Find<ModuleEntryController>(scene);if(entry)EntryLayout(entry);
                foreach(var canvas in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Canvas>(true))){
                    // Only scene-owned canvases. Shared rig/package prefab assets are never modified.
                    if(PrefabUtility.IsPartOfPrefabInstance(canvas))continue;
                    Rect(canvas.transform).sizeDelta=Resolution;var scaler=canvas.GetComponent<CanvasScaler>();if(scaler)scaler.referenceResolution=Resolution;
                }
                foreach(var f in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PlaygroundButtonFeedback>(true)))f.RestoreAuthoredAppearance();
                EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed: "+path);
                Debug.Log("[ModuleUI] Saved editable 1920 x 1080 landscape layout: "+path);
            }finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
        if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);
    }
    static void ModuleLayout(PlaygroundModuleUI ui,PlaygroundSessionRecording recording){
        ConfigureCanvas(ui.mainCanvas,.00115f);var bg=ui.title.transform.parent;Place(bg,1920,1080,0,0);
        var accent=bg.Find("Gold accent");if(accent)Place(accent,160,7,-800,520);
        Place(ui.stepLabel.transform,1420,56,-150,470);Text(ui.stepLabel,36);
        Place(ui.title.transform,1740,100,0,390);Text(ui.title,64,58);
        var hide=bg.Find("Hide instructions");if(hide)Button(hide.GetComponent<Button>(),220,64,800,470,36);
        var scroll=bg.Find("Instructions scroll area");
        if(!scroll){
            var surface=Surface("Instructions scroll area",bg,820,500,-465,80);surface.raycastTarget=true;
            scroll=surface.transform;var sr=scroll.gameObject.AddComponent<ScrollRect>();
            var viewport=new GameObject("Viewport",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(RectMask2D));viewport.layer=5;viewport.transform.SetParent(scroll,false);
            Place(viewport.transform,800,480,0,0);viewport.GetComponent<Image>().color=Color.white;
            ui.body.transform.SetParent(viewport.transform,false);var body=ui.body.rectTransform;body.anchorMin=new Vector2(0,1);body.anchorMax=new Vector2(1,1);body.pivot=new Vector2(.5f,1);body.sizeDelta=new Vector2(-24,480);body.anchoredPosition=Vector2.zero;
            sr.viewport=Rect(viewport.transform);sr.content=body;sr.horizontal=false;sr.vertical=true;sr.inertia=false;sr.movementType=ScrollRect.MovementType.Clamped;sr.scrollSensitivity=70;ui.narrativeScroll=sr;
        }
        var narrativeViewport=ui.narrativeScroll.viewport;
        narrativeViewport.anchorMin=Vector2.zero;narrativeViewport.anchorMax=Vector2.one;
        narrativeViewport.sizeDelta=new Vector2(-20,-20);narrativeViewport.anchoredPosition=Vector2.zero;
        Text(ui.body,40);ui.body.alignment=TextAlignmentOptions.TopLeft;
        ui.narrativeUp=NewButton("Scroll instructions up",bg,"Up",120,58,-280,-200,30);
        ui.narrativeDown=NewButton("Scroll instructions down",bg,"Down",160,58,-125,-200,30);
        if(recording&&recording.recordingStatusText){Place(recording.recordingStatusText.transform,500,44,-615,-200);Text(recording.recordingStatusText,28,26);}
        for(int i=0;i<ui.choices.Length;i++){var c=ui.choices[i];Button(c.button,420,180,i%2==0?255:705,245-(i/2)*190,40);c.label.alignment=TextAlignmentOptions.MidlineLeft;}
        Place(ui.status.transform,1720,90,0,-282);Text(ui.status,36,32);
        var inset=bg.Find("Status inset");if(inset)Place(inset,1780,100,0,-282);
        Place(ui.footer.transform,1740,60,0,-362);Text(ui.footer,30,28);
        Place(ui.progress.transform,1740,8,0,-403);
        Button(ui.primary,820,90,-430,-459,42);Button(ui.secondary,850,90,450,-459,42);
        Place(ui.hint.transform,1760,28,0,-523);Text(ui.hint,24);
        // Step 8 uses the right column. It never shares a slot with the question text.
        Place(ui.recordingGroup.transform,870,400,480,100);
        Place(ui.questionCount.transform,810,70,0,140);Text(ui.questionCount,36,32);
        Button(ui.recordingPlaceholder,790,96,0,30,42);Button(ui.skipQuestion,790,96,0,-100,42);
        ConfigureCanvas(ui.wristCanvas,.00025f);var wrist=ui.wristStep.transform.parent;Place(wrist,1920,1080,0,0);
        Place(ui.wristStep.transform,1760,120,0,410);Text(ui.wristStep,96,86);
        Place(ui.wristStatus.transform,1760,180,0,245);Text(ui.wristStatus,84,72);
        Button(ui.clueTool,820,200,-440,10,90);Button(ui.guessTool,820,200,440,10,90);
        Button(ui.wristHelp,1760,220,0,-330,92);
        ui.wristReturn=NewButton("Return from child view",wrist,"Return to my view",1760,220,0,-330,92);ui.wristReturn.gameObject.SetActive(false);
        ConfigureCanvas(ui.thoughtCanvas,.0008f);Place(ui.thoughtText.transform.parent,1920,1080,0,0);
        Place(ui.thoughtText.transform,1700,680,0,100);Text(ui.thoughtText,56,48);
        Button(ui.thoughtClose,720,140,470,-380,54);
        ConfigureCanvas(ui.tintCanvas,.00065f);Place(ui.tint.transform,1920,1080,0,0);
        if(recording&&recording.participantIdPanel){
            var canvas=recording.participantIdPanel.GetComponent<Canvas>();if(canvas)ConfigureCanvas(canvas,.001f);
            var field=recording.participantIdInput;
            if(field){
                Place(field.transform,1440,170,0,30);Text(field.textComponent,64,56);field.caretWidth=4;
                var viewport=field.textViewport;if(viewport){viewport.anchorMin=Vector2.zero;viewport.anchorMax=Vector2.one;viewport.sizeDelta=new Vector2(-40,-24);viewport.anchoredPosition=Vector2.zero;}
                foreach(var label in field.GetComponentsInChildren<TMP_Text>(true)){Text(label,64,56);var r=label.rectTransform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.sizeDelta=Vector2.zero;r.anchoredPosition=Vector2.zero;label.alignment=TextAlignmentOptions.MidlineLeft;}
            }
            var panel=recording.participantIdPanel.transform.Find("Panel");if(panel){
                Label("Participant heading",panel,"Enter participant ID",1720,120,0,370,72);
                Label("Participant rule",panel,"Use at least four characters, including letters and numbers. No spaces.",1740,140,0,215,42);
                Label("Participant keyboard help",panel,"Select the ID field to open the keyboard.\nChoose Done on the keyboard to start your module.",1720,180,0,-210,44);
            }
        }
        ui.PreviewTextOnlyLayout();
        EditorUtility.SetDirty(ui);
    }
    static void EntryLayout(ModuleEntryController entry){
        ConfigureCanvas(entry.menuCanvas,.0012f);var root=entry.menuCanvas.transform;
        var title=root.Find("Title").GetComponent<TMP_Text>();Place(title.transform,1740,110,0,385);Text(title,72,64);
        var subtitle=root.Find("Subtitle").GetComponent<TMP_Text>();Place(subtitle.transform,1740,70,0,285);Text(subtitle,42);
        var brand=root.Find("Module hub brand").GetComponent<TMP_Text>();Place(brand.transform,1740,60,0,480);Text(brand,34);
        for(int i=0;i<entry.moduleButtons.Length;i++){
            var b=entry.moduleButtons[i];Place(b.transform,810,260,i%2==0?-435:435,i<2?115:-165);
            foreach(var label in b.GetComponentsInChildren<TMP_Text>(true)){
                if(label.name=="Module number"){Place(label.transform,740,56,0,94);Text(label,32);}
                else if(label.name=="Module description"){Place(label.transform,740,96,0,-75);Text(label,36,34);}
                else{Place(label.transform,740,76,0,25);Text(label,56,50);}
            }
        }
        Place(entry.status.transform,1680,100,0,-365);Text(entry.status,36,32);
        var inset=root.Find("Session message inset");if(inset)Place(inset,1740,110,0,-365);
        var help=root.Find("Selection help").GetComponent<TMP_Text>();Place(help.transform,1740,65,0,-480);Text(help,34);
        EditorUtility.SetDirty(entry);
    }
}
#endif
