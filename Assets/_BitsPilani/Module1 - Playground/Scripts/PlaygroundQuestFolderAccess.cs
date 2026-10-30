using System;
using System.Threading.Tasks;
using UnityEngine;

namespace BitsPilani.Module1.Playground {
    [UnityEngine.Scripting.Preserve]
    public sealed class PlaygroundQuestFolderAccess : MonoBehaviour {
        static PlaygroundQuestFolderAccess instance;
        static AndroidJavaObject context;
        public string RootName = "Quest";
        public string TreeUri { get; private set; }
        public string LastError { get; private set; }
        public bool Ready => !string.IsNullOrEmpty(TreeUri);
        public bool IsChoosing { get; private set; }
        float nextRefresh, chooseStarted;
        public bool RefreshAccess() {
            if (Application.platform != RuntimePlatform.Android || Application.isEditor) return true;
            try {
                EnsureContext();
                using (var bridge = new AndroidJavaClass("com.bitspilani.storage.QuestStorageBridge")) TreeUri = bridge.CallStatic<string>("getTree", context, RootName);
                if (Ready) { LastError = null; IsChoosing = false; }
            } catch (Exception ex) { TreeUri = null; LastError = ex.Message; Debug.LogError("[FolderManager] Access check failed: " + ex); }
            return Ready;
        }
        void OnApplicationFocus(bool focused) { if (focused) { IsChoosing = false; RefreshAccess(); } }
        void OnApplicationPause(bool paused) { if (!paused) { IsChoosing = false; RefreshAccess(); } }
        public static PlaygroundQuestFolderAccess Get(string root) {
            if (!instance) { var go = new GameObject("Module1 Quest folder access"); instance = go.AddComponent<PlaygroundQuestFolderAccess>(); DontDestroyOnLoad(go); }
            if (instance.RootName != root) instance.TreeUri = null;
            instance.RootName = root;
            instance.RefreshAccess();
            return instance;
        }
        static void EnsureContext() {
            if (context != null) return;
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity")) context = activity.Call<AndroidJavaObject>("getApplicationContext");
        }
        public void Choose() { RefreshAccess(); Debug.Log("[FolderManager] Automatic folder creation is enabled. No picker is opened."); }
        [UnityEngine.Scripting.Preserve]
        public void OnFolderPickerResult(string result) {
            IsChoosing = false;
            if (!string.IsNullOrEmpty(result) && result.StartsWith("ok|")) { RefreshAccess(); Debug.Log("[FolderManager] Access granted to internal-storage " + RootName); }
            else { LastError = result != null && result.StartsWith("error|") ? result.Substring(6) : result; Debug.LogWarning("[FolderManager] " + LastError); }
        }
        public static async Task<PlaygroundQuestExportResult> PublishAsync(string path, PlaygroundQuestStorageSettings settings) {
            var publish = Task.Run(() => {
                AndroidJNI.AttachCurrentThread();
                try {
                    if (context == null) throw new InvalidOperationException("Quest folder access was not prepared before recording.");
                    using (var bridge = new AndroidJavaClass("com.bitspilani.storage.QuestStorageBridge")) {
                        string data = bridge.CallStatic<string>("publish", context, settings.folderName, settings.subfolderName, path, settings.displayFileName);
                        string[] parts = data.Split('\n');
                        if (parts.Length > 1) Debug.Log("[FolderManager] Headset video filename: " + parts[1]);
                        return new PlaygroundQuestExportResult { success = true, location = parts[0] };
                    }
                } catch (Exception ex) { return new PlaygroundQuestExportResult { error = ex.Message }; }
                finally { AndroidJNI.DetachCurrentThread(); }
            });
            if (await Task.WhenAny(publish, Task.Delay(20000)) != publish) return new PlaygroundQuestExportResult { error = "Shared-storage transfer timed out. The finalized recording remains queued safely." };
            return await publish;
        }
        public static async Task<PlaygroundQuestExportResult> PrepareAsync(PlaygroundQuestStorageSettings settings) {
            var prepare = Task.Run(() => {
                AndroidJNI.AttachCurrentThread();
                try {
                    if (context == null) throw new InvalidOperationException("Shared-storage context was not initialized.");
                    using (var bridge = new AndroidJavaClass("com.bitspilani.storage.QuestStorageBridge")) {
                        string uri = bridge.CallStatic<string>("prepare", context, settings.folderName, settings.subfolderName);
                        return new PlaygroundQuestExportResult { success = true, location = uri };
                    }
                } catch (Exception ex) { return new PlaygroundQuestExportResult { error = ex.Message }; }
                finally { AndroidJNI.DetachCurrentThread(); }
            });
            if (await Task.WhenAny(prepare, Task.Delay(5000)) != prepare) return new PlaygroundQuestExportResult { error = "Shared-storage folder creation timed out. Recording will continue in staging and transfer remains pending." };
            return await prepare;
        }
    }
}
