using UnityEditor;
using UnityEngine;
using BitsPilani.Module1.Playground;

namespace BitsPilani.Module1.Playground.Editor {
    [InitializeOnLoad]
    static class PlaygroundRecordingExitGuard {
        static PlaygroundRecordingExitGuard() {
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingPlayMode) Flush(); };
            AssemblyReloadEvents.beforeAssemblyReload += Flush;
        }
        static void Flush() { if (PlaygroundRecordingRun.Active) PlaygroundRecordingRun.Active.SaveBeforeEditorStops(); }
    }
    [CustomEditor(typeof(PlaygroundSessionRecording))]
    sealed class PlaygroundRecordingInspector : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            DrawDefaultInspector();
            var recording = (PlaygroundSessionRecording)target;
            EditorGUILayout.HelpBox("Assign your TMP_InputField to Participant ID Input, or set Participant Id above. IDs accept letters and numbers only. Camera view and game audio are recorded; no microphone is used.", MessageType.Info);
            if (GUILayout.Button("Open Recording Folder")) recording.OpenRecordingFolder();
            if (Application.isPlaying && GUILayout.Button("Stop and Save Video")) recording.StopAndSave();
            if (!string.IsNullOrEmpty(recording.LastSavedPath)) EditorGUILayout.SelectableLabel(recording.LastSavedPath, EditorStyles.textField);
        }
    }
}
