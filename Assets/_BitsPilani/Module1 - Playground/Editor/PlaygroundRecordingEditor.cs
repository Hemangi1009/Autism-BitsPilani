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
            EditorGUILayout.LabelField("Quest destination", "Application.persistentDataPath / " + (r.folderManager ? r.folderManager.rootFolderName : "Autism - Videos & Audios") + " / Module" + r.moduleNumber + " / <ID>");
            EditorGUILayout.LabelField("Current local folder", r.ResolveOutputFolder());
            EditorGUILayout.HelpBox("Quest APK saves directly under Application.persistentDataPath. Folders are created/reused automatically; no shared-storage permission, folder picker or ADB transfer is used. Desktop/Quest Link saves on the PC. The final path is printed in the Console.", MessageType.Info);
            if (GUILayout.Button("Open Recording Folder")) r.OpenRecordingFolder();
            if (Application.isPlaying && GUILayout.Button("Stop and Save Video")) r.StopAndSave();
            if (Application.isPlaying && r.participantIdInput && GUILayout.Button("Show ID Keyboard")) r.participantIdInput.GetComponent<PlaygroundParticipantKeyboard>()?.Open();
            if (!string.IsNullOrEmpty(r.LastSavedPath)) EditorGUILayout.SelectableLabel(r.LastSavedPath, EditorStyles.textField);
        }
    }
}
