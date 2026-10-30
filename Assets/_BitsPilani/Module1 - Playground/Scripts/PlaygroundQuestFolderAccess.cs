using System;
using System.Threading.Tasks;
using UnityEngine;

namespace BitsPilani.Module1.Playground {
    public sealed class PlaygroundQuestFolderAccess : MonoBehaviour {
        static PlaygroundQuestFolderAccess instance;
        static AndroidJavaObject context;
        public string RootName = "Quest";
        public string TreeUri { get; private set; }
        public string LastError { get; private set; }
        public bool Ready => !string.IsNullOrEmpty(TreeUri);
        public static PlaygroundQuestFolderAccess Get(string root) {
            if (!instance) { var go = new GameObject("Module1 Quest folder access"); instance = go.AddComponent<PlaygroundQuestFolderAccess>(); DontDestroyOnLoad(go); }
            instance.RootName = root;
            if (Application.platform == RuntimePlatform.Android && !Application.isEditor) {
                EnsureContext();
                using (var bridge = new AndroidJavaClass("com.bitspilani.storage.QuestStorageBridge")) instance.TreeUri = bridge.CallStatic<string>("getTree", context, root);
            }
            return instance;
        }
        static void EnsureContext() {
            if (context != null) return;
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity")) context = activity.Call<AndroidJavaObject>("getApplicationContext");
        }
        public void Choose() {
            if (Application.platform != RuntimePlatform.Android || Application.isEditor) return;
            try {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var bridge = new AndroidJavaClass("com.bitspilani.storage.QuestStorageBridge")) bridge.CallStatic("select", activity, gameObject.name, RootName);
            } catch (Exception ex) { LastError = ex.Message; Debug.LogError("[M1 QuestFolder] " + LastError); }
        }
        public void OnFolderPickerResult(string result) {
            if (result.StartsWith("ok|")) { TreeUri = result.Substring(3); LastError = null; Debug.Log("[M1 QuestFolder] Access granted to internal-storage " + RootName); }
            else { LastError = result.StartsWith("error|") ? result.Substring(6) : result; Debug.LogWarning("[M1 QuestFolder] " + LastError); }
        }
        public static Task<PlaygroundQuestExportResult> PublishAsync(string path, PlaygroundQuestStorageSettings settings) {
            return Task.Run(() => {
                AndroidJNI.AttachCurrentThread();
                try {
                    if (context == null) throw new InvalidOperationException("Quest folder access was not prepared before recording.");
                    using (var bridge = new AndroidJavaClass("com.bitspilani.storage.QuestStorageBridge")) {
                        string uri = bridge.CallStatic<string>("publish", context, settings.folderName, settings.subfolderName, path);
                        return new PlaygroundQuestExportResult { success = true, location = uri };
                    }
                } catch (Exception ex) { return new PlaygroundQuestExportResult { error = ex.Message }; }
                finally { AndroidJNI.DetachCurrentThread(); }
            });
        }
    }
}
