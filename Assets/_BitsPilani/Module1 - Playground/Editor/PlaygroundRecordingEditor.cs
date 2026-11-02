using UnityEditor;
using UnityEngine;
using BitsPilani.Module1.Playground;

namespace BitsPilani.Module1.Playground.Editor {
    [InitializeOnLoad]
    static class PlaygroundRecordingExitGuard {
        static PlaygroundRecordingExitGuard() {
            EditorApplication.playModeStateChanged += state => { if(state==PlayModeStateChange.ExitingPlayMode)Flush(); };
            AssemblyReloadEvents.beforeAssemblyReload += Flush;
        }
        static void Flush() { if(PlaygroundRecordingRun.Active)PlaygroundRecordingRun.Active.SaveBeforeEditorStops(); }
    }
    [CustomEditor(typeof(PlaygroundSessionRecording))]
    sealed class PlaygroundRecordingInspector : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            DrawDefaultInspector();
            var r = (PlaygroundSessionRecording)target;
            EditorGUILayout.LabelField("Container / codecs", "MP4 / H.264 video + AAC audio");
            EditorGUILayout.LabelField("Filename preview", r.FileNamePreview);
            EditorGUILayout.LabelField("Quest destination", "Quest selected root / " + (r.folderManager ? r.folderManager.rootFolderName : "Autism - Videos & Audios") + " / Module" + r.moduleNumber + " / <ID>");
            EditorGUILayout.LabelField("Current local folder", r.ResolveOutputFolder());
            EditorGUILayout.HelpBox("Quest APK tries the shared-storage root when FolderManager.Prefer Quest Shared Storage Root is enabled. If Android denies writing there, it automatically uses persistentDataPath. No permission/setup screen is opened. Desktop/Quest Link keeps its PC path. Current local folder shows the selected destination when running on that device.", MessageType.Info);
            if (GUILayout.Button("Open Recording Folder")) r.OpenRecordingFolder();
            if (Application.isPlaying && GUILayout.Button("Stop and Save Video")) r.StopAndSave();
            if (Application.isPlaying && r.participantIdInput && GUILayout.Button("Show ID Keyboard")) r.participantIdInput.GetComponent<PlaygroundParticipantKeyboard>()?.Open();
            if (!string.IsNullOrEmpty(r.LastSavedPath)) EditorGUILayout.SelectableLabel(r.LastSavedPath, EditorStyles.textField);
        }
    }
}
