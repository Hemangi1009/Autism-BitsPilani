#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using BitsPilani.Module1.Playground;

[CustomEditor(typeof(PlaygroundFlowController)), CanEditMultipleObjects]
public sealed class PlaygroundFlowControllerEditor : Editor {
    static readonly string[] TimingFields = {
        "initialLoadSeconds", "watchSeconds", "responseSeconds", "perspectiveSeconds",
        "replayWarningSeconds", "replayAfterPopSeconds"
    };
    static readonly string[] ExcludedFields = {
        "m_Script", "initialLoadSeconds", "watchSeconds", "responseSeconds", "perspectiveSeconds",
        "thoughtToReflectionSeconds", "replayWarningSeconds", "replayAfterPopSeconds"
    };

    public override void OnInspectorGUI() {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        EditorGUILayout.LabelField("Editable module timing", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Edit the scene component outside Play mode to save your durations. Story events scale with the phase duration; recorded VO speed and VO Delay Seconds are independent. Initial loading remains at least 6 seconds.", MessageType.Info);
        foreach (var name in TimingFields)
            EditorGUILayout.PropertyField(serializedObject.FindProperty(name));

        if (!serializedObject.isEditingMultipleObjects) {
            var flow = (PlaygroundFlowController)target;
            EditorGUILayout.HelpBox($"First observation: warning at {flow.WatchWarningTime:0.##}s; pop at {flow.WatchPopTime:0.##}s.\nResponse change at {flow.ResponseMidpointTime:0.##}s. Replay warning at {flow.ReplayEarlyWarningTime:0.##}s; pop at {flow.replayWarningSeconds:0.##}s.", MessageType.None);
        }
        EditorGUILayout.Space();
        DrawPropertiesExcluding(serializedObject, ExcludedFields);
        serializedObject.ApplyModifiedProperties();
    }
}
#endif
