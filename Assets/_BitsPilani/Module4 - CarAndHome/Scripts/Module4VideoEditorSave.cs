#if UNITY_EDITOR
using UnityEditor;
using BitsPilani.Module4;
[InitializeOnLoad]
internal static class Module4VideoEditorSave {
    static Module4VideoEditorSave(){EditorApplication.playModeStateChanged+=State;AssemblyReloadEvents.beforeAssemblyReload+=Save;}
    static void State(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)Save();}
    static void Save(){if(Module4RecordingRun.Active)Module4RecordingRun.Active.SaveBeforeEditorStops();}
}
#endif
