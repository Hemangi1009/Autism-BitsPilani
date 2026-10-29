using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BNG;
using BitsPilani.Module1.Playground;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using UIButton = UnityEngine.UI.Button;
using UIImage = UnityEngine.UI.Image;

namespace BitsPilani.Module1.Playground.Editor {
    public static class PlaygroundSceneBuilder {
        const string Folder = "Assets/_BitsPilani/Module1 - Playground";
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static Transform world, actors, targets, systems;
        static PlaygroundFlowController flow;
        static PlaygroundModuleUI ui;
        static int primitiveCount;

        [MenuItem("BitsPilani/Module 1/Create Playground Scene")]
        public static void Build() {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before creating the scene.");
            if (File.Exists(Folder + "/Scenes/Module1_Playground.unity")) throw new InvalidOperationException("The module scene already exists. It was not overwritten.");
            var sourceScene = SceneManager.GetActiveScene();
            var sourceRig = sourceScene.GetRootGameObjects().FirstOrDefault(g => g.GetComponentInChildren<BNGPlayerController>(true));
            if (!sourceRig) throw new InvalidOperationException("Open the MySample scene with its VR rig before running the builder.");
            var referenceFactory = Object.FindFirstObjectByType<FactoryManager>(FindObjectsInactive.Include);
            if (!referenceFactory) throw new InvalidOperationException("MySample FactoryManager was not found.");
            var factoryData = new SerializedObject(referenceFactory);
            var gazePrefab = factoryData.FindProperty("gazePointerPrefab").objectReferenceValue as GameObject;
            if (!gazePrefab) throw new InvalidOperationException("The reference FactoryManager has no gaze prefab.");
            foreach (string sub in new[] { "Scenes", "Prefabs", "Materials", "Audio", "Reference" }) EnsureFolder(Folder + "/" + sub);
            if (sourceScene.isDirty && !EditorSceneManager.SaveScene(sourceScene)) throw new InvalidOperationException("Could not preserve current MySample scene changes.");
            if (!string.IsNullOrEmpty(sourceScene.path)) AssetDatabase.CopyAsset(sourceScene.path, Folder + "/Reference/MySample_BeforeCreation.unity");
            var rigAsset = PrefabUtility.SaveAsPrefabAsset(sourceRig, Folder + "/Prefabs/Module1_VRRig.prefab");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            primitiveCount = 0; materials.Clear(); CreateMaterials();
            world = Group("M1 WORLD - modern housing and courtyard"); actors = Group("M1 ACTORS - grounded primitive characters");
            targets = Group("M1 STORY TARGETS"); systems = Group("M1 SYSTEMS - flow and audio");
            BuildEnvironment();
            var rig = PrefabUtility.InstantiatePrefab(rigAsset) as GameObject;
            rig.name = "Module1 VR Rig - MySample setup"; rig.transform.position = new Vector3(-6.35f, .12f, 12.65f);
            rig.transform.rotation = Quaternion.LookRotation(new Vector3(-.85f, 0, .45f));
            foreach (var canvas in rig.GetComponentsInChildren<Canvas>(true)) if (canvas.name == "DebugMenu" || canvas.name.StartsWith("LEFT HAND - module tools")) Object.DestroyImmediate(canvas.gameObject);
            foreach (var previous in rig.GetComponentsInChildren<BitsPilani.Training.Module12Training>(true)) Object.DestroyImmediate(previous);
            foreach (var previous in rig.GetComponentsInChildren<BitsPilani.Training.OriginalModule12Flow>(true)) Object.DestroyImmediate(previous);
            var player = rig.GetComponentInChildren<BNGPlayerController>();
            player.enabled = true;
            foreach (var gazePointer in rig.GetComponentsInChildren<global::Pointer>(true)) {
                gazePointer.gameObject.SetActive(true); gazePointer.enabled = true;
                foreach (var graphic in gazePointer.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
                foreach (var raycaster in gazePointer.GetComponentsInChildren<GraphicRaycaster>(true)) raycaster.enabled = false;
            }
            var locomotion = player.GetComponent<LocomotionManager>(); locomotion.DefaultLocomotion = LocomotionType.SmoothLocomotion; locomotion.LoadLocomotionFromPrefs = false;
            var factory = Group("FactoryManager - unchanged MySample gaze logic", systems).gameObject.AddComponent<FactoryManager>();
            var serializedFactory = new SerializedObject(factory); serializedFactory.FindProperty("gazePointerPrefab").objectReferenceValue = gazePrefab; serializedFactory.ApplyModifiedPropertiesWithoutUndo();
            flow = Group("Module1 Playground Flow", systems).gameObject.AddComponent<PlaygroundFlowController>();
            flow.session = flow.gameObject.AddComponent<PlaygroundSessionLog>();
            flow.rig = rig.AddComponent<PlaygroundRigAdapter>();
            flow.rig.playerRoot = player.transform; flow.rig.head = player.CenterEyeAnchor; flow.rig.cameraRig = player.CameraRig;
            flow.rig.desktopPreview = false;
            flow.rig.playerController = player; flow.rig.gravity = player.GetComponent<PlayerGravity>(); flow.rig.body = player.GetComponent<CharacterController>();
            var body = flow.rig.body; bool bodyEnabled = body.enabled; body.enabled = false;
            flow.rig.playerRoot.position = new Vector3(-6.35f, .12f + body.height * .5f - body.center.y + .02f, 12.65f);
            body.enabled = bodyEnabled;
            flow.rig.locomotionBehaviours = player.GetComponents<MonoBehaviour>().Where(c => c is LocomotionManager || c is SmoothLocomotion || c is PlayerTeleport || c is PlayerRotation).Cast<Behaviour>().ToArray();
            var eventObject = Group("Module1 VR and Desktop EventSystem", systems).gameObject;
            eventObject.AddComponent<EventSystem>(); var vrUi = eventObject.AddComponent<VRUISystem>();
            var pointers = rig.GetComponentsInChildren<UIPointer>(true);
            var rightPointer = pointers.First(p => p.ControllerSide == ControllerHand.Right); rightPointer.gameObject.SetActive(true);
            vrUi.SelectedHand = ControllerHand.Right; vrUi.RightPointerTransform = rightPointer.transform;
            vrUi.LeftPointerTransform = pointers.FirstOrDefault(p => p.ControllerSide == ControllerHand.Left)?.transform;
            vrUi.ControllerInput = new List<ControllerBinding> { ControllerBinding.RightTrigger };
            var desktopUi = eventObject.AddComponent<InputSystemUIInputModule>(); desktopUi.enabled = false;
            flow.rig.vrUi = vrUi; flow.rig.desktopUi = desktopUi; flow.rig.controllerPointers = pointers;
            var left = rig.GetComponentsInChildren<Transform>(true).First(t => t.name == "LeftController");
            BuildActorsAndTargets(); BuildUI(player.CenterEyeAnchor, left); BuildAudio();
            flow.ui = ui;
            flow.rig.moduleCanvases = new[] { ui.mainCanvas, ui.wristCanvas, ui.thoughtCanvas };
            flow.initialLoadSeconds = 6; flow.watchSeconds = 25; flow.responseSeconds = 20; flow.perspectiveSeconds = 25;
            flow.thoughtToReflectionSeconds = 4; flow.replayWarningSeconds = 15; flow.replayAfterPopSeconds = 8;
            Physics.SyncTransforms();
            foreach (var anchor in Object.FindObjectsByType<PlaygroundSurfaceAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None)) anchor.SnapToSupport();
            // Reposition view/circle markers after the final foot-to-surface placement.
            flow.childViewPoint.position = flow.child.transform.position;
            SetGround(flow.observationPoint); SetGround(flow.approachPoint); SetGround(flow.quietPoint);
            SetGround(flow.observationCircle.transform); SetGround(flow.approachCircle.transform);
            flow.observationCircle.SetActive(false); flow.approachCircle.SetActive(false);
            ui.Initialize(); ui.SetStep(0, "Loading"); ui.Show("Too loud on the slide", "Module 1 is ready. Press Play. The scene waits six seconds before the first voiceover.\n\nExplore, collect clues, test guesses, reflect, and replay once.", "Use your MySample gaze markers and right-controller UI.");
            EditorUtility.SetDirty(flow); EditorUtility.SetDirty(ui); EditorUtility.SetDirty(flow.rig); EditorUtility.SetDirty(factory);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, Folder + "/Scenes/Module1_Playground.unity")) throw new InvalidOperationException("Scene save failed.");
            if (SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.LookAt(new Vector3(0, 2, 17), Quaternion.Euler(32, 0, 0), 31, false, true);
            Selection.activeObject = flow.gameObject;
            Debug.Log($"[M1 Playground][BuildComplete] {primitiveCount} Unity primitives; scene saved at {Folder}/Scenes/Module1_Playground.unity");
        }

        static void EnsureFolder(string path) { if (AssetDatabase.IsValidFolder(path)) return; EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/')); AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path)); }
        static Transform Group(string name, Transform parent = null) { var g = new GameObject(name); g.transform.SetParent(parent, false); return g.transform; }
        static Material Mat(string name, string hex) {
            string path = Folder + "/Materials/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.name = name; ColorUtility.TryParseHtmlString(hex, out Color c); m.color = c; m.SetFloat("_Smoothness", .2f); AssetDatabase.CreateAsset(m, path); }
            materials[name] = m; return m;
        }
        static void CreateMaterials() {
            Mat("Lawn", "#81AE85"); Mat("CentralGrass", "#6E9D78"); Mat("Leaf", "#367D65"); Mat("LeafLight", "#62A17A");
            Mat("Ivory", "#F3EEE1"); Mat("Concrete", "#D4D2C7"); Mat("Path", "#DCC9A5"); Mat("Wood", "#AA7753"); Mat("Earth", "#6D5746");
            Mat("Teal", "#25868C"); Mat("Coral", "#DE806B"); Mat("Yellow", "#EBC86C"); Mat("Blue", "#73A9C3"); Mat("Lavender", "#A699B8");
            Mat("Pink", "#DCA2AE"); Mat("Navy", "#405565"); Mat("Charcoal", "#35444A"); Mat("Glass", "#7CABB3"); Mat("Skin", "#C58F70"); Mat("Hair", "#453A34");
            Mat("Rubber", "#CBAA82"); Mat("White", "#FFF9E9"); Mat("Circle", "#EEC75F");
        }
        static GameObject P(string name, PrimitiveType type, Vector3 pos, Vector3 scale, string material, Transform parent, bool solid = false) {
            var g = GameObject.CreatePrimitive(type); g.name = name; g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = materials[material]; g.GetComponent<Collider>().enabled = solid; primitiveCount++; return g;
        }
        static GameObject Box(string n, Vector3 p, Vector3 s, string m, Transform parent, bool solid = false) => P(n, PrimitiveType.Cube, p, s, m, parent, solid);
        static GameObject Sphere(string n, Vector3 p, Vector3 s, string m, Transform parent) => P(n, PrimitiveType.Sphere, p, s, m, parent);
        static GameObject Cylinder(string n, Vector3 p, float radius, float height, string m, Transform parent, bool solid = false) => P(n, PrimitiveType.Cylinder, p, new Vector3(radius * 2, height * .5f, radius * 2), m, parent, solid);
        static void Rod(string n, Vector3 a, Vector3 b, float radius, string m, Transform parent) {
            var g = Cylinder(n, (a + b) * .5f, radius, Vector3.Distance(a, b), m, parent); g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
        }
        static GameObject Floor(string n, Vector3 p, Vector3 s, string m) { var g = Box(n, p, s, m, world, true); g.AddComponent<PlaygroundWalkableSurface>(); return g; }
        static void Grounded(Transform root, params Renderer[] bases) { var a = root.gameObject.AddComponent<PlaygroundSurfaceAnchor>(); a.contactParts = bases; }
        static void SetGround(Transform t) { if (PlaygroundSurfaceAnchor.TrySupport(t.position, out float y)) t.position = new Vector3(t.position.x, y, t.position.z); }

        static void BuildEnvironment() {
            Floor("Courtyard ground - top at zero", new Vector3(0, -.2f, 17), new Vector3(55, .4f, 47), "Lawn");
            Floor("Central lawn", new Vector3(0, .0125f, 18), new Vector3(7.2f, .025f, 17), "CentralGrass");
            Floor("Entrance walkway", new Vector3(0, .03f, 3), new Vector3(3.2f, .06f, 14), "Path");
            Floor("Cross courtyard walkway", new Vector3(0, .03f, 9.5f), new Vector3(39, .06f, 2.4f), "Path");
            Floor("Rear walkway", new Vector3(0, .03f, 29), new Vector3(39, .06f, 2.4f), "Path");
            foreach (float x in new[] { -18.5f, 18.5f }) Floor("Side walkway", new Vector3(x, .03f, 19), new Vector3(2.2f, .06f, 21), "Path");
            Floor("Play area safety surface", new Vector3(-8.8f, .06f, 18), new Vector3(13.8f, .12f, 15), "Rubber");
            Floor("Small birthday patio", new Vector3(8.8f, .06f, 15.5f), new Vector3(9.2f, .12f, 7.4f), "Ivory");
            for (int i = 0; i < 7; i++) Box("Play mat edge colour", new Vector3(-14.3f + i * 1.75f, .126f, 10.9f), new Vector3(1.6f, .012f, .55f), i % 2 == 0 ? "Coral" : "Yellow", world);
            Slide("Main yellow twin slide", new Vector3(-7.8f, .12f, 18), 0, 2.1f, "Yellow", 2);
            Slide("Coral junior slide", new Vector3(-13f, .12f, 17.2f), -28, 1.3f, "Coral", 1);
            Slide("Blue side slide", new Vector3(-11.2f, .12f, 22.2f), 70, 2.4f, "Blue", 1);
            Swing(new Vector3(-6.8f, .12f, 23.5f)); Seesaw(new Vector3(-12.5f, 0, 7));
            Bench("Aunty bench", new Vector3(1.7f, .025f, 13), 90);
            Bench("Garden bench", new Vector3(3, .025f, 24), -90);
            Bench("Playground bench", new Vector3(-17, 0, 18), 90);
            Bench("Party-side bench", new Vector3(16, 0, 10), -20);
            foreach (float x in new[] { -22.8f, 22.8f }) { Building(new Vector3(x, 0, 10), x < 0 ? -90 : 90, x < 0 ? "Ivory" : "Concrete", 4); Building(new Vector3(x, 0, 25), x < 0 ? -90 : 90, x < 0 ? "Concrete" : "Ivory", 4); }
            Building(new Vector3(-13, 0, 36), 0, "Ivory", 4); Building(new Vector3(0, 0, 37), 0, "Concrete", 5); Building(new Vector3(13, 0, 36), 0, "Ivory", 4);
            foreach (Vector2 p in new[] { new Vector2(-17, 2), new Vector2(-9, 2), new Vector2(9, 2), new Vector2(17, 2), new Vector2(-16, 28), new Vector2(-8, 32), new Vector2(8, 32), new Vector2(16, 28), new Vector2(16, 21) }) Tree(p.x, p.y);
            for (int i = 0; i < 10; i++) { float x = -17.5f + i * 3.9f; if (Mathf.Abs(x) >= 3.5f) FlowerBed(x, -3.2f); }
            foreach (float x in new[] { -20f, 20f }) for (int i = 0; i < 9; i++) {
                float z = 1 + i * 3.8f; Box("Boundary pier", new Vector3(x, .5f, z), new Vector3(.3f, 1, .3f), "Ivory", world, true);
                if (i < 8) { Box("Boundary base", new Vector3(x, .16f, z + 1.9f), new Vector3(.18f, .32f, 3.8f), "Concrete", world, true); Box("Boundary rail", new Vector3(x, .75f, z + 1.9f), new Vector3(.07f, .07f, 3.8f), "Charcoal", world); }
            }
            foreach (float x in new[] { -2.2f, 2.2f }) { Box("Entry sign post", new Vector3(x, 1.35f, -1.4f), new Vector3(.23f, 2.7f, .23f), "Teal", world, true); Sphere("Entry post cap", new Vector3(x, 2.8f, -1.4f), Vector3.one * .28f, "Ivory", world); }
            Box("Society entrance sign", new Vector3(0, 2.55f, -1.4f), new Vector3(4.3f, .55f, .12f), "Teal", world);
            WorldText("Courtyard sign lettering", world, new Vector3(0, 2.55f, -1.48f), "SOCIETY GARDENS", .01f, new Vector2(400, 44), 30, TextAlignmentOptions.Center);
            var light = new GameObject("Courtyard sunlight").AddComponent<Light>(); light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(42, -28, 0); light.color = new Color(1, .95f, .85f); light.intensity = 1.25f; light.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight; RenderSettings.ambientSkyColor = new Color(.65f, .76f, .83f); RenderSettings.ambientEquatorColor = new Color(.57f, .65f, .62f); RenderSettings.ambientGroundColor = new Color(.38f, .43f, .38f);
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(.76f, .84f, .86f); RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 55; RenderSettings.fogEndDistance = 110;
            var skyShader = Shader.Find("Skybox/Procedural"); if (skyShader) { var sky = new Material(skyShader); sky.SetColor("_SkyTint", new Color(.55f, .69f, .78f)); sky.SetFloat("_Exposure", .8f); AssetDatabase.CreateAsset(sky, Folder + "/Materials/DaylightSky.mat"); RenderSettings.skybox = sky; }
        }

        static void Slide(string name, Vector3 position, float yaw, float height, string colour, int lanes) {
            var t = Group(name, world); t.position = position; t.rotation = Quaternion.Euler(0, yaw, 0); float width = lanes == 2 ? 2.35f : 1.4f;
            Box("Platform", new Vector3(0, height, 0), new Vector3(width + .3f, .16f, 1.7f), "Wood", t, true);
            foreach (float x in new[] { -width / 2, width / 2 }) foreach (float z in new[] { -.65f, .65f }) Cylinder("Ground-to-roof post", new Vector3(x, (height + 1.24f) / 2, z), .075f, height + 1.24f, "Teal", t, true);
            Box("Supported canopy roof", new Vector3(0, height + 1.3f, 0), new Vector3(width + .7f, .14f, 2.1f), colour, t);
            for (int lane = 0; lane < lanes; lane++) {
                float x = lanes == 2 ? (lane == 0 ? -.58f : .58f) : 0;
                Vector3 top = new Vector3(x, height, -.8f), end = new Vector3(x, .3f, -3.8f);
                var chute = Box("Slide chute", (top + end) / 2, new Vector3(1, .1f, Vector3.Distance(top, end)), colour, t, true); chute.transform.localRotation = Quaternion.LookRotation(end - top);
                foreach (float edge in new[] { -.51f, .51f }) Rod("Slide edge", top + new Vector3(edge, .13f, 0), end + new Vector3(edge, .13f, 0), .05f, colour, t);
                Box("Supported landing", new Vector3(x, .14f, -4.05f), new Vector3(1.04f, .28f, .65f), "Teal", t, true); Box("Landing surface", new Vector3(x, .3f, -4.05f), new Vector3(1.07f, .05f, .65f), colour, t, true);
            }
            for (int i = 0; i < 7; i++) { float f = (i + 1f) / 7; Box("Stair tread", new Vector3(0, height * f - .1f, 3.15f - 2.4f * f), new Vector3(1.05f, .18f, .42f), "Ivory", t, true); }
            foreach (float x in new[] { -.48f, .48f }) { Rod("Stair structural support", new Vector3(x, .03f, 3.4f), new Vector3(x, height - .1f, .55f), .10f, "Teal", t); Rod("Stair handrail", new Vector3(x, .7f, 3.2f), new Vector3(x, height + .75f, .8f), .045f, "Teal", t); }
            foreach (float x in new[] { -width / 2, width / 2 }) Rod("Platform side rail", new Vector3(x, height + .8f, -.65f), new Vector3(x, height + .8f, .65f), .04f, "Ivory", t);
        }
        static void Swing(Vector3 position) {
            var t = Group("Static two-seat swings", world); t.position = position;
            foreach (float x in new[] { -1.9f, 1.9f }) { Rod("Swing A frame", new Vector3(x, 0, -.8f), new Vector3(x, 2.65f, 0), .08f, "Teal", t); Rod("Swing A frame", new Vector3(x, 0, .8f), new Vector3(x, 2.65f, 0), .08f, "Teal", t); }
            Rod("Swing crossbeam", new Vector3(-2.1f, 2.65f, 0), new Vector3(2.1f, 2.65f, 0), .09f, "Coral", t);
            foreach (float x in new[] { -.85f, .85f }) { Box("Hanging swing seat", new Vector3(x, .6f, 0), new Vector3(.6f, .1f, .35f), "Yellow", t); foreach (float dx in new[] { -.24f, .24f }) Rod("Supporting swing rope", new Vector3(x + dx, .65f, 0), new Vector3(x + dx, 2.65f, 0), .014f, "Ivory", t); }
        }
        static void Seesaw(Vector3 position) {
            var t = Group("Static seesaw", world); t.position = position;
            Cylinder("Grounded seesaw pivot", new Vector3(0, .3f, 0), .3f, .6f, "Teal", t, true); Box("Seesaw beam", new Vector3(0, .63f, 0), new Vector3(3.2f, .13f, .4f), "Coral", t);
            foreach (float x in new[] { -1.3f, 1.3f }) { Box("Seesaw seat", new Vector3(x, .73f, 0), new Vector3(.55f, .08f, .55f), "Yellow", t); Rod("Seesaw handle", new Vector3(x, .77f, 0), new Vector3(x, 1.02f, 0), .03f, "Teal", t); }
        }
        static void Bench(string name, Vector3 pos, float yaw) {
            var t = Group(name, world); t.position = pos; t.rotation = Quaternion.Euler(0, yaw, 0);
            var bases = new List<Renderer>();
            foreach (float x in new[] { -.8f, .8f }) { bases.Add(Box("Bench grounded leg", new Vector3(x, .25f, 0), new Vector3(.1f, .5f, .65f), "Charcoal", t, true).GetComponent<Renderer>()); Box("Back upright", new Vector3(x, .8f, .31f), new Vector3(.08f, 1.1f, .08f), "Charcoal", t); }
            for (int i = 0; i < 4; i++) Box("Bench seat slat", new Vector3(0, .5f, -.24f + i * .16f), new Vector3(2.1f, .09f, .12f), "Wood", t, true);
            for (int i = 0; i < 3; i++) Box("Bench back slat", new Vector3(0, .85f + i * .16f, .32f), new Vector3(2.1f, .1f, .08f), "Wood", t);
            Grounded(t, bases.ToArray());
        }
        static void Building(Vector3 pos, float yaw, string colour, int floors) {
            var t = Group("Modern society apartment block", world); t.position = pos; t.rotation = Quaternion.Euler(0, yaw, 0); float h = floors * 2.6f;
            Box("Main apartment volume", new Vector3(0, h / 2, 0), new Vector3(8.5f, h, 5.4f), colour, t, true);
            Box("Facade accent strip", new Vector3(-3.55f, h / 2, -2.72f), new Vector3(.7f, h, .12f), "Wood", t);
            Box("Ground plinth", new Vector3(0, .22f, 0), new Vector3(8.8f, .44f, 5.7f), "Concrete", t);
            Box("Roof shade", new Vector3(0, h + .08f, 0), new Vector3(9.1f, .16f, 6), "Ivory", t);
            for (int f = 0; f < floors; f++) for (int c = 0; c < 3; c++) {
                float x = -2.45f + c * 2.5f, y = 1.45f + f * 2.6f;
                Box("Window frame", new Vector3(x, y, -2.73f), new Vector3(1.45f, 1.55f, .1f), "Ivory", t); Box("Blue window glazing", new Vector3(x, y, -2.8f), new Vector3(1.22f, 1.3f, .06f), "Glass", t);
                Box("Balcony slab", new Vector3(x, y - .77f, -3), new Vector3(1.7f, .12f, .72f), "Ivory", t); Box("Balcony front", new Vector3(x, y - .38f, -3.33f), new Vector3(1.7f, .66f, .065f), "Charcoal", t);
            }
            Box("Entry door", new Vector3(0, 1, -2.83f), new Vector3(1, 2, .12f), "Wood", t); Box("Roof equipment", new Vector3(2, h + .535f, .8f), new Vector3(1.3f, .75f, 1), "Concrete", t);
        }
        static void Tree(float x, float z) {
            var t = Group("Grounded planter tree", world); t.position = new Vector3(x, 0, z);
            var planter = Cylinder("Planter base", new Vector3(0, .12f, 0), 1, .24f, "Ivory", t, true);
            Cylinder("Soil", new Vector3(0, .235f, 0), .88f, .04f, "Earth", t);
            Cylinder("Supported trunk", new Vector3(0, 1.55f, 0), .15f, 2.65f, "Wood", t, true);
            Sphere("Round canopy", new Vector3(0, 3.15f, 0), new Vector3(2.5f, 2.5f, 2.5f), "Leaf", t);
            Sphere("Canopy accent", new Vector3(.65f, 3.55f, .12f), new Vector3(1.65f, 1.7f, 1.65f), "LeafLight", t);
            Grounded(t, planter.GetComponent<Renderer>());
        }
        static void FlowerBed(float x, float z) {
            var t = Group("Entrance flower planter", world); t.position = new Vector3(x, 0, z); var bed = Box("Planter base", new Vector3(0, .15f, 0), new Vector3(2.5f, .3f, 1), "Ivory", t);
            for (int i = 0; i < 4; i++) { Sphere("Shrub", new Vector3(-.82f + i * .55f, .56f, 0), new Vector3(.65f, .54f, .7f), i % 2 == 0 ? "Leaf" : "LeafLight", t); Sphere("Small flower", new Vector3(-.82f + i * .55f, .83f, -.06f), Vector3.one * .12f, i % 2 == 0 ? "Coral" : "Yellow", t); }
            Grounded(t, bed.GetComponent<Renderer>());
        }

        static Transform Person(string name, Vector3 pos, string shirt, float scale, float yaw, bool seated = false) {
            var t = Group(name, actors); t.position = pos; t.rotation = Quaternion.Euler(0, yaw, 0); t.localScale = Vector3.one * scale;
            float hip = seated ? .65f : .86f;
            P("Shirt torso", PrimitiveType.Capsule, new Vector3(0, hip + .3f, 0), new Vector3(.48f, .38f, .32f), shirt, t);
            Cylinder("Neck", new Vector3(0, hip + .65f, 0), .08f, .19f, "Skin", t);
            Sphere("Head", new Vector3(0, hip + .91f, 0), new Vector3(.38f, .43f, .36f), "Skin", t);
            Sphere("Hair", new Vector3(0, hip + 1.04f, .025f), new Vector3(.4f, .22f, .37f), "Hair", t);
            foreach (float x in new[] { -.075f, .075f }) Sphere("Eye", new Vector3(x, hip + .94f, -.173f), new Vector3(.032f, .038f, .02f), "Charcoal", t);
            Sphere("Nose", new Vector3(0, hip + .86f, -.185f), new Vector3(.055f, .065f, .06f), "Skin", t);
            var feet = new List<Renderer>();
            foreach (float x in new[] { -.14f, .14f }) {
                if (seated) { Rod("Thigh", new Vector3(x, hip, 0), new Vector3(x, hip, -.4f), .105f, "Navy", t); Rod("Shin", new Vector3(x, hip, -.4f), new Vector3(x, .14f, -.4f), .085f, "Navy", t); }
                else Rod("Leg", new Vector3(x, hip, 0), new Vector3(x, .14f, 0), .095f, "Navy", t);
                feet.Add(Box("Shoe", new Vector3(x, .075f, seated ? -.48f : -.07f), new Vector3(.21f, .15f, .32f), "Ivory", t).GetComponent<Renderer>());
            }
            foreach (float x in new[] { -.29f, .29f }) {
                Rod("Upper arm", new Vector3(x, hip + .5f, 0), new Vector3(x * 1.2f, hip + .15f, -.02f), .075f, shirt, t);
                Rod("Forearm", new Vector3(x * 1.2f, hip + .15f, -.02f), new Vector3(x * 1.1f, hip - .05f, -.12f), .065f, "Skin", t);
                Sphere("Hand", new Vector3(x * 1.1f, hip - .05f, -.12f), Vector3.one * .15f, "Skin", t);
            }
            Grounded(t, feet.ToArray()); return t;
        }

        static void BuildActorsAndTargets() {
            var child = Person("Your child - static pose states", new Vector3(-7.2f, .12f, 13.1f), "Teal", .72f, -10);
            flow.child = child.gameObject.AddComponent<PlaygroundCharacterPose>(); flow.child.torso = child.Find("Shirt torso");
            flow.child.upperArms = child.Cast<Transform>().Where(t => t.name == "Upper arm").ToArray(); flow.child.forearms = child.Cast<Transform>().Where(t => t.name == "Forearm").ToArray(); flow.child.hands = child.Cast<Transform>().Where(t => t.name == "Hand").ToArray(); flow.child.grounding = child.GetComponent<PlaygroundSurfaceAnchor>();
            var defenders = Group("Ear defenders - static optional support", child); flow.child.earDefenders = defenders.gameObject;
            foreach (float x in new[] { -.215f, .215f }) Sphere("Ear cup", new Vector3(x, 1.77f, .01f), new Vector3(.1f, .24f, .2f), "Blue", defenders);
            Rod("Head band", new Vector3(-.2f, 1.97f, 0), new Vector3(.2f, 1.97f, 0), .035f, "Navy", defenders); defenders.gameObject.SetActive(false);
            var aunty = Person("Aunty on bench", new Vector3(1.7f, .025f, 13), "Lavender", 1, 90, true);
            Person("Child at junior slide", new Vector3(-14.8f, .12f, 15.3f), "Pink", .67f, -25); Person("Child waiting for swings", new Vector3(-4.7f, .12f, 23.2f), "Coral", .73f, 65);
            Person("Child near blue slide", new Vector3(-10, .12f, 25), "Yellow", .7f, 110);
            Person("Party child one", new Vector3(8.1f, .12f, 14.3f), "Yellow", .68f, 20); Person("Party child two", new Vector3(10.4f, .12f, 13.7f), "Coral", .7f, 40);
            Person("Party child three", new Vector3(7, .12f, 17.3f), "Blue", .72f, 140); Person("Birthday host", new Vector3(12.2f, .12f, 17.2f), "Teal", 1, 70);
            Person("Neighbour near lawn", new Vector3(3.1f, .025f, 22), "Pink", 1, -50); Person("Neighbour at entrance", new Vector3(4.1f, 0, 6.8f), "Blue", .97f, 20);
            var party = Group("Minimal birthday party - table cake balloons speaker", world); party.position = new Vector3(8.8f, .12f, 15.5f);
            foreach (float x in new[] { -1.2f, 1.2f }) foreach (float z in new[] { -.45f, .45f }) Box("Grounded table leg", new Vector3(x, .45f, z + 1), new Vector3(.11f, .9f, .11f), "Wood", party, true);
            Box("Table top", new Vector3(0, .94f, 1), new Vector3(3.1f, .12f, 1.3f), "Ivory", party, true); Box("Table runner", new Vector3(0, 1.007f, 1), new Vector3(1.0f, .014f, 1.31f), "Coral", party);
            Cylinder("Cake platter", new Vector3(0, 1.05f, 1), .56f, .07f, "Yellow", party); Cylinder("Cake lower tier", new Vector3(0, 1.25f, 1), .44f, .34f, "Pink", party); Cylinder("Cake top tier", new Vector3(0, 1.51f, 1), .29f, .2f, "White", party);
            for (int i = 0; i < 4; i++) { float a = i * Mathf.PI / 2; Cylinder("Cake candle", new Vector3(Mathf.Cos(a) * .17f, 1.72f, 1 + Mathf.Sin(a) * .17f), .018f, .22f, "Teal", party); }
            for (int i = 0; i < 6; i++) { var flag = Box("Table bunting", new Vector3(-1.25f + i * .5f, .76f, .32f), new Vector3(.19f, .23f, .04f), new[] { "Teal", "Yellow", "Coral" }[i % 3], party); flag.transform.localRotation = Quaternion.Euler(0, 0, 45); }
            Rod("Bunting cord attached to table", new Vector3(-1.5f, .88f, .32f), new Vector3(1.5f, .88f, .32f), .009f, "Ivory", party);
            var speaker = Group("Party speaker - grounded", world); speaker.position = new Vector3(5.4f, .12f, 13.3f); Vector3 speakerToChild = child.position - speaker.position; speakerToChild.y = 0; speaker.rotation = Quaternion.LookRotation(-speakerToChild);
            var speakerBase = Box("Speaker ground base", new Vector3(0, .05f, 0), new Vector3(.9f, .1f, .7f), "Teal", speaker, true);
            Box("Speaker cabinet", new Vector3(0, .8f, 0), new Vector3(.72f, 1.4f, .55f), "Charcoal", speaker, true);
            foreach (float y in new[] { .5f, 1.13f }) { var cone = Cylinder("Speaker cone", new Vector3(0, y, -.3f), .23f, .05f, "Navy", speaker); cone.transform.localRotation = Quaternion.Euler(90, 0, 0); Sphere("Speaker dust cap", new Vector3(0, y, -.34f), Vector3.one * .13f, "Charcoal", speaker); }
            Grounded(speaker, speakerBase.GetComponent<Renderer>());
            flow.child.soundSource = speaker;
            var balloons = Group("Party balloon cluster - tethered", world); balloons.position = new Vector3(11.3f, .12f, 16.8f);
            var weight = Cylinder("Balloon cluster ground weight", new Vector3(0, .1f, 0), .25f, .2f, "Teal", balloons);
            for (int i = 0; i < 5; i++) { Vector3 p = new Vector3((i - 2) * .31f, 1.8f + (i % 2) * .35f, (i % 2) * .15f); Sphere("Tethered balloon", p, new Vector3(.43f, .55f, .43f), new[] { "Yellow", "Pink", "Coral", "Blue", "Lavender" }[i], balloons); Rod("Attached balloon string", new Vector3(0, .2f, 0), p - new Vector3(0, .275f, 0), .006f, "Ivory", balloons); }
            Grounded(balloons, weight.GetComponent<Renderer>()); flow.perspectiveProps = new[] { speaker, balloons };
            flow.partyFocus = Group("Birthday party gaze focus", targets); flow.partyFocus.position = new Vector3(8.8f, 1.5f, 15.5f);
            var eventRoot = Group("Balloon event - static until pop", world); eventRoot.position = new Vector3(-4.8f, .12f, 13.2f);
            var eventWeight = Cylinder("Balloon ground weight", new Vector3(0, .1f, 0), .16f, .2f, "Coral", eventRoot);
            flow.eventBalloon = Sphere("Balloon to pop", new Vector3(0, 1.85f, 0), new Vector3(.44f, .57f, .44f), "Coral", eventRoot);
            flow.balloonString = Cylinder("Balloon event tether", new Vector3(0, .8825f, 0), .006f, 1.365f, "Ivory", eventRoot);
            flow.popOrigin = Group("Balloon pop VFX origin", eventRoot); flow.popOrigin.localPosition = new Vector3(0, 1.85f, 0); Grounded(eventRoot, eventWeight.GetComponent<Renderer>());
            flow.observationPoint = Group("Observation circle destination", targets); flow.observationPoint.position = new Vector3(-3.5f, .12f, 11.5f);
            flow.approachPoint = Group("Child approach - about one metre", targets); flow.approachPoint.position = new Vector3(-6.05f, .12f, 12.8f);
            flow.quietPoint = Group("Quieter response destination", targets); flow.quietPoint.position = new Vector3(-14.2f, .12f, 12.5f); flow.quietPoint.rotation = Quaternion.identity;
            flow.childViewPoint = Group("Child perspective floor anchor", targets); flow.childViewPoint.position = child.position;
            flow.observationCircle = Circle("Observation circle highlight", flow.observationPoint.position, .85f); flow.approachCircle = Circle("Child approach circle highlight", flow.approachPoint.position, .65f);
            flow.gazeChild = Group("Gaze_Child", targets); flow.gazeChild.position = child.position + Vector3.up * 1.65f;
            flow.gazeOnlooker = Group("Gaze_Onlooker", targets); flow.gazeOnlooker.position = aunty.position + Vector3.up * 2.1f;
            flow.gazeParty = Group("Gaze_Party", targets); flow.gazeParty.position = new Vector3(8.8f, 2.2f, 16.2f);
            flow.gazeThought = Group("Gaze_ThoughtBubble", targets); flow.gazeThought.position = aunty.position + Vector3.up * 2.1f;
            // Orient gaze targets toward the entry so their canvas faces the arriving parent.
            foreach (var g in new[] { flow.gazeChild, flow.gazeOnlooker, flow.gazeParty, flow.gazeThought }) { Vector3 away = g.position - new Vector3(-6.35f, 1.77f, 12.65f); away.y = 0; g.rotation = Quaternion.LookRotation(away); g.localScale = Vector3.one; }
        }
        static GameObject Circle(string name, Vector3 pos, float radius) {
            var t = Group(name, targets); t.position = pos;
            for (int i = 0; i < 32; i++) { float a = i * Mathf.PI * 2 / 32; Box("Ground highlight segment", new Vector3(Mathf.Cos(a) * radius, .008f, Mathf.Sin(a) * radius), new Vector3(.12f, .016f, .12f), "Circle", t); }
            return t.gameObject;
        }

        static RectTransform Rect(string n, Transform parent, Vector2 size, Vector2 pos) { var g = new GameObject(n, typeof(RectTransform)); g.layer = 5; var r = g.GetComponent<RectTransform>(); r.SetParent(parent, false); r.sizeDelta = size; r.anchoredPosition = pos; return r; }
        static UIImage Panel(string n, Transform parent, Vector2 size, Vector2 pos, Color colour) { var r = Rect(n, parent, size, pos); var image = r.gameObject.AddComponent<UIImage>(); image.color = colour; return image; }
        static TMP_Text Text(string n, Transform parent, Vector2 size, Vector2 pos, string content, float fontSize, Color colour, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft) {
            var r = Rect(n, parent, size, pos); var text = r.gameObject.AddComponent<TextMeshProUGUI>(); text.font = TMP_Settings.defaultFontAsset; text.fontSize = fontSize; text.text = content; text.color = colour; text.alignment = alignment; text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Truncate; return text;
        }
        static TMP_Text WorldText(string n, Transform parent, Vector3 p, string content, float scale, Vector2 size, float fs, TextAlignmentOptions alignment) { var c = Canvas(n, parent, size, scale); c.transform.localPosition = p; return Text("Text", c.transform, size, Vector2.zero, content, fs, Color.white, alignment); }
        static UIButton Button(string n, Transform parent, Vector2 size, Vector2 pos, string caption, out TMP_Text label) {
            var image = Panel(n, parent, size, pos, new Color(.12f, .35f, .36f)); var button = image.gameObject.AddComponent<UIButton>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(.65f, .95f, .83f); colors.pressedColor = new Color(.4f, .8f, .68f); colors.disabledColor = new Color(.42f, .48f, .50f, .7f); button.colors = colors;
            label = Text("Label", image.transform, size - new Vector2(28, 6), Vector2.zero, caption, 26, Color.white, TextAlignmentOptions.Center); return button;
        }
        static Canvas Canvas(string n, Transform parent, Vector2 size, float scale) { var r = Rect(n, parent, size, Vector2.zero); r.localScale = Vector3.one * scale; var c = r.gameObject.AddComponent<Canvas>(); c.renderMode = RenderMode.WorldSpace; c.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent; var scaler = r.gameObject.AddComponent<CanvasScaler>(); scaler.dynamicPixelsPerUnit = 10; r.gameObject.AddComponent<GraphicRaycaster>(); return c; }

        static void BuildUI(Transform head, Transform left) {
            var root = Group("M1 UI - step-wise panels"); ui = root.gameObject.AddComponent<PlaygroundModuleUI>(); ui.head = head; ui.leftHand = left;
            ui.mainCanvas = Canvas("Main step panel - VR world canvas", root, new Vector2(1000, 960), .0011f); ui.mainCanvas.transform.position = new Vector3(1.2f, 1.55f, .3f);
            var bg = Panel("Dark panel", ui.mainCanvas.transform, new Vector2(1000, 960), Vector2.zero, new Color(.065f, .13f, .18f, .98f));
            Panel("Gold accent", bg.transform, new Vector2(920, 6), new Vector2(0, 458), new Color(.94f, .75f, .35f));
            ui.stepLabel = Text("Step number", bg.transform, new Vector2(900, 35), new Vector2(0, 423), "MODULE 1", 24, new Color(.95f, .8f, .48f));
            ui.title = Text("Step heading", bg.transform, new Vector2(900, 65), new Vector2(0, 365), "Too loud on the slide", 37, Color.white);
            ui.body = Text("Instructions", bg.transform, new Vector2(900, 140), new Vector2(0, 280), "", 28, new Color(.9f, .94f, .94f)); ui.body.enableAutoSizing = true; ui.body.fontSizeMin = 22; ui.body.fontSizeMax = 28;
            ui.status = Text("Step status", bg.transform, new Vector2(900, 64), new Vector2(0, 176), "", 24, new Color(.58f, .85f, .78f));
            ui.choices = new PlaygroundChoiceSlot[6];
            for (int i = 0; i < 6; i++) { TMP_Text label; var b = Button("Choice " + (i + 1), bg.transform, new Vector2(900, 54), new Vector2(0, 109 - i * 61), "Choice", out label); ui.choices[i] = new PlaygroundChoiceSlot { button = b, label = label }; }
            ui.footer = Text("Selection summary", bg.transform, new Vector2(900, 65), new Vector2(0, -275), "", 21, new Color(.76f, .83f, .85f));
            var track = Panel("Time progress", bg.transform, new Vector2(900, 7), new Vector2(0, -321), new Color(.21f, .31f, .36f)); ui.progress = track.gameObject.AddComponent<UnityEngine.UI.Slider>(); ui.progress.interactable = false; ui.progress.minValue = 0; ui.progress.maxValue = 1;
            var fill = Panel("Time progress fill", track.transform, new Vector2(900, 7), Vector2.zero, new Color(.95f, .76f, .35f)); fill.rectTransform.sizeDelta = Vector2.zero; fill.rectTransform.anchoredPosition = Vector2.zero; ui.progress.fillRect = fill.rectTransform; ui.progress.targetGraphic = fill;
            ui.primary = Button("Primary step action", bg.transform, new Vector2(510, 66), new Vector2(-195, -378), "Continue", out ui.primaryLabel);
            ui.secondary = Button("Secondary action", bg.transform, new Vector2(350, 66), new Vector2(275, -378), "Back", out ui.secondaryLabel);
            ui.hint = Text("Control hint", bg.transform, new Vector2(900, 40), new Vector2(0, -442), "Right controller: point and release trigger to select.", 19, new Color(.6f, .72f, .76f));
            var recording = Panel("Step 8 recording placeholder only", bg.transform, new Vector2(900, 150), new Vector2(0, -184), new Color(.10f, .20f, .26f)); ui.recordingGroup = recording.gameObject;
            ui.questionCount = Text("Question counter", recording.transform, new Vector2(840, 32), new Vector2(0, 46), "QUESTION 1 OF 3", 23, new Color(.95f, .8f, .48f));
            TMP_Text ignored; ui.recordingPlaceholder = Button("Recording not implemented", recording.transform, new Vector2(570, 58), new Vector2(-130, -14), "Record answer - coming later", out ignored); ui.recordingPlaceholder.interactable = false;
            ui.skipQuestion = Button("Skip this reflection question", recording.transform, new Vector2(215, 58), new Vector2(310, -14), "Skip", out ignored);
            ui.wristCanvas = Canvas("Left-hand module tools", left, new Vector2(540, 300), .00058f); ui.wristCanvas.transform.localPosition = new Vector3(-.015f, .13f, .08f);
            var wrist = Panel("Wrist background", ui.wristCanvas.transform, new Vector2(540, 300), Vector2.zero, new Color(.055f, .13f, .18f));
            ui.wristStep = Text("Wrist step", wrist.transform, new Vector2(490, 56), new Vector2(0, 102), "MODULE 1", 25, new Color(.96f, .81f, .46f));
            ui.wristStatus = Text("Wrist status", wrist.transform, new Vector2(490, 60), new Vector2(0, 42), "Loading...", 23, Color.white);
            ui.clueTool = Button("Clue tool", wrist.transform, new Vector2(235, 58), new Vector2(-128, -29), "Clue tool", out ignored); ui.guessTool = Button("Guess tool", wrist.transform, new Vector2(235, 58), new Vector2(128, -29), "Guess tool", out ignored);
            ui.wristHelp = Button("Show instructions", wrist.transform, new Vector2(490, 48), new Vector2(0, -105), "Show instructions", out ignored);
            var aunty = actors.Find("Aunty on bench"); ui.thoughtCanvas = Canvas("Aunty thought panel", root, new Vector2(850, 470), .0014f); ui.thoughtCanvas.transform.position = aunty.position + Vector3.up * 2.25f;
            var thought = Panel("Thought bubble panel", ui.thoughtCanvas.transform, new Vector2(850, 470), Vector2.zero, new Color(.95f, .93f, .86f));
            ui.thoughtText = Text("Onlooker thought", thought.transform, new Vector2(760, 310), new Vector2(0, 45), "", 31, new Color(.14f, .22f, .27f)); ui.thoughtClose = Button("Close thought bubble", thought.transform, new Vector2(300, 60), new Vector2(210, -175), "Close", out ignored);
            ui.tintCanvas = Canvas("Child perspective static tint", head, new Vector2(1400, 1400), .0005f); ui.tintCanvas.transform.localPosition = new Vector3(0, 0, .2f); ui.tintCanvas.sortingOrder = 200;
            ui.tint = Panel("Static amber tint - no flashes", ui.tintCanvas.transform, new Vector2(1400, 1400), Vector2.zero, new Color(1, .64f, .18f, .055f)); ui.tint.raycastTarget = false; Object.DestroyImmediate(ui.tintCanvas.GetComponent<GraphicRaycaster>());
        }

        static AudioSource Audio(string name, Transform parent, Vector3 local, string clip, bool loop, float spatial) {
            var t = Group(name, parent); t.localPosition = local; var a = t.gameObject.AddComponent<AudioSource>(); a.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/Audio/" + clip + ".wav"); a.loop = loop; a.playOnAwake = false; a.spatialBlend = spatial; a.minDistance = 6; a.maxDistance = 42; a.rolloffMode = AudioRolloffMode.Linear; return a;
        }
        static void BuildAudio() {
            flow.voice = Audio("Narration", systems, Vector3.zero, "VO_01_Enter", false, 0); flow.voice.volume = .8f;
            var speaker = world.Find("Party speaker - grounded"); flow.partyMusic = Audio("Party music", speaker, Vector3.up, "PartyMusic", true, .85f);
            flow.crowd = Audio("Crowd ambience", world, new Vector3(8.8f, 1.4f, 15), "CrowdAmbience", true, .75f);
            flow.pop = Audio("Balloon pop sound", flow.popOrigin, Vector3.zero, "BalloonPop", false, .85f); flow.pop.volume = .45f; flow.pop.minDistance = 4;
            flow.stepVoiceovers = new AudioClip[10]; string[] names = { "", "VO_01_Enter", "VO_02_Watch", "VO_03_Clues", "VO_04_Guesses", "VO_05_Respond", "VO_06_ChildView", "VO_07_Onlooker", "VO_08_Reflect", "VO_09_Replay" };
            for (int i = 1; i < names.Length; i++) flow.stepVoiceovers[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/Audio/" + names[i] + ".wav");
            flow.changeVoice = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/Audio/VO_02_Change.wav"); flow.responseVoice = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/Audio/VO_05_Observe.wav"); flow.replayHintVoice = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/Audio/VO_09_EarlySigns.wav"); flow.summaryVoice = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/Audio/VO_10_Summary.wav");
        }
    }
}
