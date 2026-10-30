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
        static void RetryTransfers() {
            foreach (var recorder in Object.FindObjectsByType<PlaygroundSessionRecording>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) if (recorder.saveToQuest) recorder.RetryPendingQuestTransfers();
        }
    }
    [CustomEditor(typeof(PlaygroundSessionRecording))]
    sealed class PlaygroundRecordingInspector : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            DrawDefaultInspector();
            var recording = (PlaygroundSessionRecording)target;
            EditorGUILayout.HelpBox("Assign the TMP input or set Participant Id. Quest destination is Internal shared storage / Movies / Quest Folder Name. APK publishes on-device; Quest Link transfers via USB debugging. Pending transfers retain the staging video until verified.", MessageType.Info);
            if (GUILayout.Button("Open Temporary / PC Recording Folder")) recording.OpenRecordingFolder();
            if (recording.saveToQuest && GUILayout.Button("Send Pending Videos to Quest")) recording.RetryPendingQuestTransfers();
            if (Application.isPlaying && GUILayout.Button("Stop and Save Video")) recording.StopAndSave();
            if (!string.IsNullOrEmpty(recording.LastSavedPath)) EditorGUILayout.SelectableLabel(recording.LastSavedPath, EditorStyles.textField);
        }
    }
}
