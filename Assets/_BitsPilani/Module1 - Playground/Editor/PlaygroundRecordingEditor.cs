using UnityEditor;
using UnityEngine;
using BitsPilani.Module1.Playground;

namespace BitsPilani.Module1.Playground.Editor {
    [InitializeOnLoad]
    static class PlaygroundRecordingExitGuard {
        static PlaygroundRecordingExitGuard() {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.ExitingPlayMode) Flush();
                if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += RetryTransfers;
            };
            AssemblyReloadEvents.beforeAssemblyReload += Flush;
        }
        static void Flush() { if (PlaygroundRecordingRun.Active) PlaygroundRecordingRun.Active.SaveBeforeEditorStops(); }
        static void RetryTransfers() { foreach (var r in Object.FindObjectsByType<PlaygroundSessionRecording>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) if (r.saveToQuest) r.RetryPendingQuestTransfers(); }
    }
    [CustomEditor(typeof(PlaygroundSessionRecording))]
    sealed class PlaygroundRecordingInspector : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            DrawDefaultInspector();
            var r = (PlaygroundSessionRecording)target;
            EditorGUILayout.LabelField("Container / codecs", "MP4 / H.264 video + AAC audio");
            EditorGUILayout.LabelField("Filename preview", r.FileNamePreview);
            EditorGUILayout.LabelField("Headset destination", "Internal shared storage/" + r.questFolderName + "/" + r.questSubfolderName);
            EditorGUILayout.HelpBox("APK: grant access once to the root Quest folder using Choose Quest folder in the module. Its recording subfolder is created/reused automatically. Quest Link: transfers using USB debugging. No silent fallback to Movies or another folder.", MessageType.Info);
            if (GUILayout.Button("Open Temporary / PC Recording Folder")) r.OpenRecordingFolder();
            if (r.saveToQuest && GUILayout.Button("Send Pending Videos to Quest")) r.RetryPendingQuestTransfers();
            if (Application.isPlaying && GUILayout.Button("Stop and Save Video")) r.StopAndSave();
            if (Application.isPlaying && r.participantIdInput && GUILayout.Button("Show ID Keyboard")) r.participantIdInput.GetComponent<PlaygroundParticipantKeyboard>()?.Open();
            if (!string.IsNullOrEmpty(r.LastSavedPath)) EditorGUILayout.SelectableLabel(r.LastSavedPath, EditorStyles.textField);
        }
    }
}
