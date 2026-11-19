using UnityEditor;
using UnityEngine;

namespace BitsPilani.Shared.Editor
{
    // Keep the scene's serialized fields editable. Never apply runtime appearance presets.
    [CustomEditor(typeof(ChildViewSensoryEffects))]
    public sealed class ChildViewSensoryEffectsInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var effects = (ChildViewSensoryEffects)target;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Currently active", effects.IsRunning);
                EditorGUILayout.FloatField("Current pulse", effects.CurrentPulse);
            }
        }
    }
}
