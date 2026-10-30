using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using BNG;
using Button = UnityEngine.UI.Button;

namespace BitsPilani.Module1.Playground.Editor {
    public static class ModuleHubSceneBuilder {
        const string Module = "Assets/_BitsPilani/Module1 - Playground";
        const string Entry = "Assets/_BitsPilani/Module Selection/Scenes/Autism_ModuleEntry.unity";
        static Material Mat(string name) { return AssetDatabase.LoadAssetAtPath<Material>(Module + "/Materials/" + name + ".mat"); }
        static void Primitive(string name, Vector3 position, Vector3 scale, string material) {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=Mat(material);
        }
        static TMP_Text Text(Transform parent,string name,string content,Vector2 position,Vector2 size,float font,Color color) {
            var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);var rect=(RectTransform)go.transform;rect.anchoredPosition=position;rect.sizeDelta=size;
            var text=go.AddComponent<TextMeshProUGUI>();text.font=TMP_Settings.defaultFontAsset;text.text=content;text.fontSize=font;text.color=color;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;return text;
        }
        static FolderManager Manager(PlaygroundSessionRecording reference) {
            var go=new GameObject("FolderManager - shared module storage");var manager=go.AddComponent<FolderManager>();manager.rootFolderName="Autism - Videos & Audios";manager.adbExecutable=reference.adbExecutable;manager.questDeviceSerial=reference.questDeviceSerial;manager.saveToQuest=reference.saveToQuest;return manager;
        }
        [MenuItem("BitsPilani/Modules/Create module entry scene")]
        public static void Build() {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            if(File.Exists(Entry))throw new InvalidOperationException("Entry scene already exists; it was not overwritten.");
            EditorSceneManager.SaveOpenScenes();var scene=SceneManager.GetActiveScene();string moduleScenePath=scene.path;
            var flow=UnityEngine.Object.FindFirstObjectByType<PlaygroundFlowController>();if(!flow)throw new InvalidOperationException("Open Module1_Playground first.");
            var recording=flow.recording;var manager=UnityEngine.Object.FindFirstObjectByType<FolderManager>();if(!manager)manager=Manager(recording);
            manager.rootFolderName="Autism - Videos & Audios";
            recording.folderManager=manager;recording.moduleNumber=1;recording.questFolderName=manager.rootFolderName;recording.questSubfolderName="Module1";
            recording.returnToEntryOnCompletion=true;recording.filenameFormat="Video_{timestamp}";recording.timestampFormat="yyyy-MM-dd_HH:mm:ss";
            if(recording.participantIdInput){var canvas=recording.participantIdInput.GetComponentInParent<Canvas>();recording.participantIdPanel=canvas&&canvas!=flow.ui.mainCanvas?canvas.gameObject:recording.participantIdInput.gameObject;recording.participantIdInput.interactable=true;if(recording.participantIdInput.placeholder is TMP_Text placeholder)placeholder.text="ID, e.g. AB12 (4+ letters/numbers)";EditorUtility.SetDirty(recording.participantIdInput);}
            EditorUtility.SetDirty(recording);EditorUtility.SetDirty(manager);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            string adb=recording.adbExecutable,serial=recording.questDeviceSerial;bool toQuest=recording.saveToQuest;
            Directory.CreateDirectory("Assets/_BitsPilani/Module Selection/Scenes");AssetDatabase.Refresh();
            var hub=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.skybox=Mat("DaylightSky");RenderSettings.ambientLight=new Color(.72f,.78f,.82f);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            Primitive("Lobby floor",new Vector3(0,-.1f,0),new Vector3(16,.2f,16),"Concrete");
            Primitive("Menu backdrop",new Vector3(0,1.8f,3.35f),new Vector3(6.8f,3.6f,.2f),"Navy");
            Primitive("Gold header trim",new Vector3(0,3.55f,3.19f),new Vector3(6.8f,.055f,.055f),"Yellow");
            Primitive("Teal left column",new Vector3(-3.55f,1.7f,3.4f),new Vector3(.22f,3.4f,.4f),"Teal");
            Primitive("Teal right column",new Vector3(3.55f,1.7f,3.4f),new Vector3(.22f,3.4f,.4f),"Teal");
            var light=new GameObject("Lobby sunlight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(45,-20,0);
            var storage=new GameObject("FolderManager - shared module storage").AddComponent<FolderManager>();storage.adbExecutable=adb;storage.questDeviceSerial=serial;storage.saveToQuest=toQuest;
            var eventObject=new GameObject("Module selection EventSystem");eventObject.AddComponent<EventSystem>();var vrUi=eventObject.AddComponent<VRUISystem>();var desktop=eventObject.AddComponent<InputSystemUIInputModule>();desktop.enabled=false;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Module+"/Prefabs/Module1_VRRig.prefab");var rig=(GameObject)PrefabUtility.InstantiatePrefab(prefab);rig.name="Entry VR rig";
            foreach(var old in rig.GetComponentsInChildren<MonoBehaviour>(true))if(old&&(old.GetType().Name=="Module12Training"||old.GetType().Name=="OriginalModule12Flow"||old is PlaygroundRigAdapter))old.enabled=false;
            foreach(var canvas in rig.GetComponentsInChildren<Canvas>(true))if(canvas.name.Contains("DebugMenu")||canvas.name.StartsWith("LEFT HAND -"))canvas.gameObject.SetActive(false);
            foreach(var fade in rig.GetComponentsInChildren<HeadCollisionFade>(true))fade.enabled=false;
            var player=rig.GetComponentInChildren<BNGPlayerController>(true);player.enabled=true;
            var body=player.GetComponent<CharacterController>();body.enabled=false;player.transform.SetPositionAndRotation(new Vector3(0,body.height*.5f-body.center.y+.02f,0),Quaternion.identity);body.enabled=true;
            var drivers=player.GetComponents<MonoBehaviour>().Where(b=>b is LocomotionManager||b is SmoothLocomotion||b is PlayerTeleport||b is PlayerRotation).Cast<Behaviour>().ToArray();foreach(var b in drivers)b.enabled=false;
            var pointers=rig.GetComponentsInChildren<UIPointer>(true);var right=pointers.First(p=>p.ControllerSide==ControllerHand.Right);right.gameObject.SetActive(true);vrUi.RightPointerTransform=right.transform;vrUi.LeftPointerTransform=pointers.FirstOrDefault(p=>p.ControllerSide==ControllerHand.Left)?.transform;
            var uiRoot=new GameObject("Module selection canvas",typeof(RectTransform));uiRoot.layer=5;var ui=uiRoot.AddComponent<Canvas>();ui.renderMode=RenderMode.WorldSpace;uiRoot.AddComponent<GraphicRaycaster>();var rectRoot=(RectTransform)uiRoot.transform;rectRoot.sizeDelta=new Vector2(1280,1040);rectRoot.localScale=Vector3.one*.0025f;rectRoot.position=new Vector3(0,1.7f,1.9f);
            var background=uiRoot.AddComponent<Image>();background.color=new Color(.045f,.10f,.15f,.99f);
            Text(uiRoot.transform,"Title","Autism training",new Vector2(0,430),new Vector2(1180,90),54,new Color(.95f,.81f,.46f));
            Text(uiRoot.transform,"Subtitle","Choose your module",new Vector2(0,350),new Vector2(1180,60),36,Color.white);
            var controller=uiRoot.AddComponent<ModuleEntryController>();controller.folderManager=storage;controller.menuCanvas=ui;controller.vrUi=vrUi;controller.rightPointer=right;controller.moduleButtons=new Button[4];
            for(int i=0;i<4;i++){
                var go=new GameObject("Module "+(i+1)+" button",typeof(RectTransform));go.layer=5;go.transform.SetParent(uiRoot.transform,false);var rect=(RectTransform)go.transform;rect.sizeDelta=new Vector2(1100,132);rect.anchoredPosition=new Vector2(0,220-i*165);
                var image=go.AddComponent<Image>();image.color=i==0?new Color(.09f,.39f,.37f):new Color(.16f,.21f,.25f);var button=go.AddComponent<Button>();button.targetGraphic=image;button.interactable=i==0;var colors=button.colors;colors.disabledColor=new Color(.55f,.59f,.62f);button.colors=colors;controller.moduleButtons[i]=button;
                Text(go.transform,"Module label",i==0?"MODULE 1   |   Playground":"MODULE "+(i+1)+"   |   Coming soon",Vector2.zero,new Vector2(1040,110),34,i==0?Color.white:new Color(.7f,.76f,.8f));
            }
            controller.status=Text(uiRoot.transform,"Status","Module 1 is available. Other modules are not ready yet.",new Vector2(0,-460),new Vector2(1180,80),21,new Color(.75f,.85f,.88f));
            var adapter=rig.AddComponent<PlaygroundRigAdapter>();adapter.playerRoot=player.transform;adapter.head=player.CenterEyeAnchor;adapter.cameraRig=player.CameraRig;adapter.playerController=player;adapter.gravity=player.GetComponent<PlayerGravity>();adapter.body=body;adapter.locomotionBehaviours=drivers;adapter.vrUi=vrUi;adapter.desktopUi=desktop;adapter.controllerPointers=pointers;adapter.moduleCanvases=new[]{ui};adapter.desktopPreview=true;
            controller.gameObject.AddComponent<EntryMovementLock>().rig=adapter;
            EditorSceneManager.MarkSceneDirty(hub);EditorSceneManager.SaveScene(hub,Entry);
            var existing=EditorBuildSettings.scenes.Where(s=>s.path!=Entry&&s.path!=moduleScenePath).ToList();existing.Insert(0,new EditorBuildSettingsScene(moduleScenePath,true));existing.Insert(0,new EditorBuildSettingsScene(Entry,true));EditorBuildSettings.scenes=existing.ToArray();
            AssetDatabase.SaveAssets();Selection.activeGameObject=uiRoot;
            if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAt(new Vector3(0,1.7f,1.9f),Quaternion.identity,3,false,true);
            Debug.Log("[ModuleHub] Entry scene saved; Module 1 linked; Modules 2-4 disabled; build scene order updated.");
        }
    }
}
