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
            Instance = this; DontDestroyOnLoad(gameObject); CreateLocalModuleFolders();
        }
        void Start() { if (Instance == this) Debug.Log("[FolderManager] Recording root: " + LocalRoot); }
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
        public void CreateLocalModuleFolders() { for (int i = 1; i <= 4; i++) Directory.CreateDirectory(Path.Combine(LocalRoot, "Module" + i)); }
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
