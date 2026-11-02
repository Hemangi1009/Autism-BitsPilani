#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BitsPilani.Module1.Playground;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Explicit, idempotent migration. No ExecuteAlways/OnValidate styling can overwrite designer edits.
public static class PlaygroundCanvasAuthoring {
    const string Module = "Assets/_BitsPilani/Module1 - Playground/";
    static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<T>(true)).FirstOrDefault();
    [MenuItem("Tools/Bits Pilani/UI/Prepare editable scene canvases")]
    public static void Prepare() {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before editing canvases.");
        var original = SceneManager.GetActiveScene();
        string asset = Module + "UI/ModuleUIRounded.asset";
        if (!AssetDatabase.IsValidFolder(Module + "UI")) AssetDatabase.CreateFolder(Module.TrimEnd('/'), "UI");
        var sprite = AssetDatabase.LoadAllAssetsAtPath(asset).OfType<Sprite>().FirstOrDefault();
        if (!sprite) {
            var texture = Object.Instantiate(PlaygroundUIStyle.Rounded.texture);
            texture.name = "Module UI rounded corners"; texture.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(texture, asset);
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect, Vector4.one * 24);
            sprite.name = "Module rounded panel"; sprite.hideFlags = HideFlags.None;
            AssetDatabase.AddObjectToAsset(sprite, texture); AssetDatabase.SaveAssets();
        }
        PlaygroundUIStyle.UseRoundedAsset(sprite);
        foreach (var path in new[] { Module + "Scenes/Module1_Playground.unity", "Assets/_BitsPilani/Module Selection/Scenes/Autism_ModuleEntry.unity" }) {
            var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try {
                var ui = Find<PlaygroundModuleUI>(scene);
                var entry = Find<ModuleEntryController>(scene);
                bool changed = false;
                if (ui && !ui.useAuthoredCanvas) {
                    var recording = Find<PlaygroundSessionRecording>(scene);
                    if (recording) ui.sessionVideoStatus = recording.recordingStatusText;
                    ui.BakeCanvasForEditing();
                    if (recording && !recording.useAuthoredIdPanel) {
                        PlaygroundUIStyle.LightPanel(recording.participantIdPanel);
                        recording.useAuthoredIdPanel = true;
                        EditorUtility.SetDirty(recording);
                    }
                    EditorUtility.SetDirty(ui); changed = true;
                }
                if (entry && !entry.useAuthoredCanvas) { entry.BakeCanvasForEditing(); EditorUtility.SetDirty(entry); changed = true; }
                if (changed) {
                    foreach (var feedback in scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<PlaygroundButtonFeedback>(true))) feedback.RestoreAuthoredAppearance();
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + path);
                }
                Debug.Log("[ModuleUI] Scene-authored UI ready: " + path + ". Edit its RectTransforms, Images and TMP labels normally; Play mode preserves the design.");
            } finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
    }
}
#endif
