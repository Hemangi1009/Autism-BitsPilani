using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BitsPilani.Module1.Playground {
    [DefaultExecutionOrder(-500)]
    public sealed class FolderManager : MonoBehaviour {
        public static FolderManager Instance { get; private set; }
        public string rootFolderName = "Autism - Videos & Audios";
        public string entrySceneName = "Autism_ModuleEntry";
        public string[] moduleSceneNames = { "Module1_Playground", "", "", "" };
        public bool[] moduleAvailable = { true, false, false, false };
        [HideInInspector] public bool saveToQuest = false; // Retired transfer setting.
        [HideInInspector] public string adbExecutable;
        [HideInInspector] public string questDeviceSerial;
        public int SelectedModule { get; private set; } = 1;
        public string ParticipantId { get; private set; }
        public string LastStorageError { get; private set; }
        public string LastSessionMessage { get; private set; }
        public bool NeedsAndroidStorageSetup => false; // Folder creation is automatic; no in-app setup.
        public bool HasStorageAccess => true; // Navigation must not wait on storage authorization.
        public void OpenAdministratorStorageSetup() { Debug.Log("[FolderManager] Folder creation is automatic; no setup screen is opened."); }
        bool loading;
        public bool IsLoading => loading;

        void Awake() {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject); _ = PrepareModuleFoldersAsync();
        }
        void Start() { if (Instance == this) LogUnityStoragePaths(); }
        [ContextMenu("Log Unity Storage Paths")]
        public void LogUnityStoragePaths() {
            string runtime = Application.platform == RuntimePlatform.Android && !Application.isEditor
                ? "Quest/Android APK: these are device paths."
                : Application.platform + ": these are host paths. Quest Link runs on the PC, so it does not report headset storage paths.";
            LogStoragePath("Application.persistentDataPath", Application.persistentDataPath,
                "Writable app storage on Quest; suitable for recorded videos.", runtime, true);
            LogStoragePath("Application.dataPath", Application.dataPath,
                "Quest APK: points to the installed APK (or OBB), not a writable recording directory.", runtime, false);
            LogStoragePath("Application.streamingAssetsPath", Application.streamingAssetsPath,
                "Quest APK: read-only assets inside the APK/JAR, not writable recording storage.", runtime, false);
        }
        static string PathPreview(string parent, string child) {
            if (string.IsNullOrEmpty(parent)) return "<unavailable>";
            // These are display-only paths and may contain the <ID> placeholder.
            return parent.TrimEnd('/', '\\') + "/" + child.Replace('\\', '/');
        }
        void LogStoragePath(string api, string basePath, string suitability, string runtime, bool showRecordingRoot) {
            string root = PathPreview(basePath, rootFolderName);
            string session = PathPreview(PathPreview(root, "Module" + SelectedModule),
                string.IsNullOrEmpty(ParticipantId) ? "<ID>" : ParticipantId);
            Debug.Log("[UnityPath] " + api
                + "\nRuntime: " + runtime
                + "\nBase path (" + (basePath == null ? 0 : basePath.Length) + " characters): " + basePath
                + "\nRoot folder preview (" + root.Length + " characters): " + root
                + "\nSession folder preview: " + session
                + "\nQuest saving: " + suitability
                + (showRecordingRoot ? "\nActual configured recording root: " + LocalRoot : "")
                + "\nComparison only: no folders are created at these preview paths.", this);
        }

        public string PersistentRoot => Path.Combine(Application.persistentDataPath, rootFolderName);
        public static bool IsValidId(string id) { return !string.IsNullOrEmpty(id) && Regex.IsMatch(id, "\\A(?=.*[A-Za-z])(?=.*[0-9])[A-Za-z0-9]{4,64}\\z"); }
        public string LocalRoot {
            get {
                string parent = Application.platform == RuntimePlatform.Android && !Application.isEditor ? Application.persistentDataPath : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (string.IsNullOrEmpty(parent)) parent = Application.persistentDataPath;
                return Application.platform == RuntimePlatform.Android && !Application.isEditor ? PersistentRoot : Path.Combine(parent, rootFolderName);
                // return Path.Combine("/sdcard", rootFolderName);
            }
        }
        public void CreateLocalModuleFolders() { for (int i=1;i<=4;i++) { string folder=Path.Combine(LocalRoot,"Module"+i); try { Directory.CreateDirectory(folder); } catch(Exception ex) { LastStorageError="Cannot create recording folder: "+folder+"; "+ex.Message; Debug.LogError("[StorageError] Operation: create module folder\nPath: "+folder+"\n"+ex); throw; } } }
        [ContextMenu("Print Last Storage Error Log")]
        public void PrintLastStorageErrorLog() { PlaygroundStorageDiagnostics.PrintSavedLog(); }
        public bool IsAvailable(int module) { return module >= 1 && module <= 4 && moduleAvailable.Length >= module && moduleAvailable[module - 1] && moduleSceneNames.Length >= module && !string.IsNullOrEmpty(moduleSceneNames[module - 1]); }
        public PlaygroundQuestStorageSettings Settings(string relative = "") {
            return new PlaygroundQuestStorageSettings { enabled = false, android = Application.platform == RuntimePlatform.Android && !Application.isEditor,
                folderName = rootFolderName, subfolderName = relative, adbExecutable = adbExecutable, deviceSerial = questDeviceSerial, keepLocalCopy = false };
        }
        public Task<bool> PrepareModuleFoldersAsync() {
            try { CreateLocalModuleFolders(); LastStorageError=null; return Task.FromResult(true); }
            catch(Exception ex) { LastStorageError=ex.Message; Debug.LogError("[FolderManager] " + ex); return Task.FromResult(false); }
        }
        public Task<string> PrepareParticipantAsync(int module, string id) {
            if (module < 1 || module > 4 || !IsValidId(id)) throw new ArgumentException("ID must have 4-64 letters/numbers, include a letter and a number, and contain no spaces.");
            SelectedModule=module; ParticipantId=id; CreateLocalModuleFolders();
            string folder=Path.Combine(LocalRoot,"Module"+module,id);
            Directory.CreateDirectory(folder); LastStorageError=null;
            Debug.Log("[FolderManager] Saving session to: " + folder);
            return Task.FromResult(folder);
        }
        public string SessionRelativeFolder(int module, string id) {
            if (module < 1 || module > 4 || !IsValidId(id)) throw new ArgumentException("Invalid module or participant ID.");
            return "Module" + module + "/" + id;
        }
        public string FutureAudioFileName(DateTime time) { return "Audio_" + time.ToString("yyyy-MM-dd_HH:mm:ss") + ".wav"; }
        public async void OpenModule(int module) {
            if (loading || !IsAvailable(module)) return;

            loading = true; SelectedModule = module; ParticipantId = null;
            try { var load = SceneManager.LoadSceneAsync(moduleSceneNames[module - 1]); while (!load.isDone) await Task.Yield(); }
            finally { loading = false; }
        }
        public async Task ReturnToEntryAsync(PlaygroundSessionRecording recording = null) {
            if (loading) return;
            loading = true;
            try {
                if (recording) await recording.StopAndSaveAsync("Return to module entry");
                LastSessionMessage = !recording || string.IsNullOrEmpty(recording.LastSavedPath) ? "Returned to module selection." : recording.QuestTransferPending ? "Last video is retained; headset transfer is pending." : "Last session saved in Module" + SelectedModule + "/" + ParticipantId + ".";
                ParticipantId = null;
                var load = SceneManager.LoadSceneAsync(entrySceneName); while (!load.isDone) await Task.Yield();
            } finally { loading = false; }
        }
        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
