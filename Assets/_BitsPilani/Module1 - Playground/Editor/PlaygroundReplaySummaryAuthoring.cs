#if UNITY_EDITOR
using System;
using System.Linq;
using BitsPilani.Module1.Playground;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PlaygroundReplaySummaryAuthoring {
    const string Module="Assets/_BitsPilani/Module1 - Playground/";
    static RectTransform Rect(Transform t)=>t as RectTransform;
    static void Place(Transform t,float w,float h,float x,float y){var r=Rect(t);r.anchorMin=r.anchorMax=r.pivot=Vector2.one*.5f;r.sizeDelta=new Vector2(w,h);r.anchoredPosition=new Vector2(x,y);}
    static GameObject Node(string name,Transform parent){var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);Undo.RegisterCreatedObjectUndo(go,"Create authored replay/summary UI");return go;}
    static Image Panel(string name,Transform parent,float w,float h,float x,float y,Color color){var go=Node(name,parent);Place(go.transform,w,h,x,y);var image=go.AddComponent<Image>();PlaygroundUIStyle.Surface(image,color,true);image.raycastTarget=false;return image;}
    static TMP_Text Label(string name,Transform parent,TMP_FontAsset font,string value,float w,float h,float x,float y,float size,Color color,bool center=false){
        var go=Node(name,parent);Place(go.transform,w,h,x,y);var text=go.AddComponent<TextMeshProUGUI>();text.font=font;text.text=value;text.fontStyle=FontStyles.Bold;text.fontSize=size;
        text.enableAutoSizing=true;text.fontSizeMin=size-4;text.fontSizeMax=size;text.color=color;text.alignment=center?TextAlignmentOptions.Center:TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Overflow;text.margin=new Vector4(4,4,4,4);text.raycastTarget=false;return text;
    }
    [MenuItem("Tools/Bits Pilani/UI/Create editable replay alert and summary table")]
    public static void Prepare(){
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode before authoring the UI.");
        var original=SceneManager.GetActiveScene();var path=Module+"Scenes/Module1_Playground.unity";var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
        if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        try{
            var ui=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PlaygroundModuleUI>(true)).Single();Undo.RecordObject(ui,"Assign authored replay and summary UI");
            var sprite=AssetDatabase.LoadAllAssetsAtPath(Module+"UI/ModuleUIRounded.asset").OfType<Sprite>().First();PlaygroundUIStyle.UseRoundedAsset(sprite);
            if(!ui.replayAttentionCanvas){
                var root=Node("Replay attention - scene authored",ui.head);root.transform.localPosition=new Vector3(0,.1f,1.2f);root.transform.localRotation=Quaternion.identity;root.transform.localScale=Vector3.one*.00045f;Rect(root.transform).sizeDelta=new Vector2(1920,1080);
                var canvas=root.AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=ui.head.GetComponent<Camera>();canvas.sortingOrder=110;
                var panel=Panel("Attention card",root.transform,1640,400,0,220,Color.white);PlaygroundUIStyle.CardShadow(panel,.13f,7);
                Label("Attention heading",panel.transform,ui.title.font,"REPLAY - ACT EARLY",1500,90,0,130,64,Color.black,true);
                Label("Attention message",panel.transform,ui.title.font,"Look at your child.\nNotice the early signs and help prevent a meltdown.",1500,230,0,-30,52,Color.black,true);
                ui.replayAttentionCanvas=canvas;root.SetActive(false);
            }
            if(!ui.summaryTableRoot){
                var root=Node("Summary comparison table - scene authored",ui.title.transform.parent);Place(root.transform,1740,500,0,80);ui.summaryTableRoot=root;ui.summaryRows=new PlaygroundSummaryRow[4];
                string[] titles={"Comparison","Response","Timing","Outcome","Clues / guesses"};float[] heights={80,110,85,125,90};float[] widths={330,685,685};float[] positions={-695,-177.5f,517.5f};float top=245;
                for(int row=0;row<5;row++){
                    float y=top-heights[row]*.5f;top-=heights[row];
                    if(row>0)ui.summaryRows[row-1]=new PlaygroundSummaryRow();
                    for(int col=0;col<3;col++){
                        Color fill=row==0?Color.black:col==0?new Color(.9f,.9f,.9f):row%2==0?new Color(.965f,.965f,.965f):Color.white;
                        var cell=Panel("Row "+row+" column "+col,root.transform,widths[col],heights[row],positions[col],y,fill);
                        var edge=cell.GetComponent<Outline>();if(edge)edge.effectColor=new Color(0,0,0,.12f);
                        string caption=row==0?(col==0?"Comparison":col==1?"First attempt":"Replay (1 of 1)"):col==0?titles[row]:"Recorded session value";
                        var label=Label("Cell text",cell.transform,ui.title.font,caption,widths[col]-28,heights[row]-12,0,0,row==0?36:col==0?34:36,row==0?Color.white:Color.black,row==0);
                        if(row>0&&col==1)ui.summaryRows[row-1].firstAttempt=label;if(row>0&&col==2)ui.summaryRows[row-1].replay=label;
                    }
                }
                root.SetActive(false);
            }
            EditorUtility.SetDirty(ui);EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("The authored UI scene could not be saved.");
            Debug.Log("[M1 UI] Replay alert and first-attempt/replay comparison table are saved scene objects. Activate them in the Hierarchy or use the UI's Summary comparison table preview menu to edit.");
        }finally{if(opened)EditorSceneManager.CloseScene(scene,true);if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);}
    }
}
#endif
