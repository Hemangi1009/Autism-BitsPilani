using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace BitsPilani.Module1.Playground {
    public enum PlaygroundVideoFormat { MP4_H264_AAC }
    public sealed class PlaygroundSessionRecording : MonoBehaviour {
        [Header("Session recording")]
        public bool recordModule = true;
        [Tooltip("Keep false to pause the module with Retry when video recording cannot start.")]
        public bool allowModuleWithoutRecording = false;
        public Camera recordingCamera;
        public AudioListener gameAudioListener;
        [Header("Module folder and navigation")]
        public FolderManager folderManager;
        [Range(1,4)] public int moduleNumber = 1;
        public GameObject participantIdPanel;
        public bool returnToEntryOnCompletion = true;
        [Header("Output folder")]
        [Tooltip("An absolute folder path, or a folder name under Documents on desktop. Created if missing; existing folders are reused.")]
        public string outputFolder = "BitsPilaniRecordings";
        // Retired shared-storage settings are retained only for old scene serialization.
        [HideInInspector] public bool saveToQuest;
        [HideInInspector] public string questFolderName = "Quest";
        [Tooltip("Created inside the internal-storage Quest folder, or reused if it already exists.")]
        [HideInInspector] public string questSubfolderName = "bitspilani";
        [Tooltip("Retain a PC/app-private duplicate after successful headset publication. Failed/pending transfers always retain the source.")]
        [HideInInspector] public bool keepLocalCopyAfterQuestSave;
        [Tooltip("For Quest Link on PC only: Android SDK/platform-tools/adb.exe. Not used by an APK running on Quest.")]
        [HideInInspector] public string adbExecutable;
        [Tooltip("Optional. Set this when multiple Android devices are connected.")]
        [HideInInspector] public string questDeviceSerial;
        [Header("Participant identity")]
        [Tooltip("Letters and numbers only, 1 to 64 characters. Used when Participant ID Input is unassigned.")]
        public string participantId = "Participant001";
        [Tooltip("Assign your canvas's TMP_InputField here. Its current text takes priority over the Inspector ID.")]
        public TMP_InputField participantIdInput;
        public TMP_Text recordingStatusText;
        public bool showVirtualKeyboard = true;
        [Header("Video format and file naming")]
        public PlaygroundVideoFormat videoFormat = PlaygroundVideoFormat.MP4_H264_AAC;
        [Tooltip("Keep {id} and {timestamp}. The .mp4 extension is added automatically.")]
        public string filenameFormat = "Video_{timestamp}";
        public string timestampFormat = "yyyy-MM-dd_HH:mm:ss";
        [Header("Video quality")]
        public Vector2Int videoResolution = new Vector2Int(1280, 720);
        [Range(10, 60)] public int framesPerSecond = 30;
        public string LastError { get; private set; }
        public string LastSavedPath { get; private set; }
        public string LastQuestSavedPath { get; private set; }
        public bool QuestTransferPending => false;
        public string LockedParticipantId { get; private set; }
        public bool IsRecording => run && run.IsRecording;
        public bool IsSaving => run && run.IsSaving;
        PlaygroundRecordingRun run;
        Task<bool> startTask;
        bool idSubmitted;

        PlaygroundParticipantKeyboard keyboard;

        public bool IsQuestFolderReady => true; // Actual directory writes are probed asynchronously, never via a setup gate.
        public string FileNamePreview { get { try { return BuildFileName(IsValidParticipantId(participantId) ? participantId : "ID", DateTime.Now).Replace(':','-') + ".mp4"; } catch (Exception ex) { return ex.Message; } } }
        public string BuildFileName(string id, DateTime time) {
            if (string.IsNullOrEmpty(filenameFormat) || !filenameFormat.Contains("{timestamp}")) throw new ArgumentException("Filename Format must contain {timestamp}.");
            string name = filenameFormat.Replace("{id}", id).Replace("{timestamp}", time.ToString(timestampFormat));
            if (name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4);
            if (name.Length > 180 || !Regex.IsMatch(name, "\\A[A-Za-z0-9_.:-]+\\z")) throw new ArgumentException("Filename format can use letters, numbers, underscore, colon, hyphen and dot only.");
            return name;
        }
        public void SelectQuestFolder() {
            if (Application.platform != RuntimePlatform.Android || Application.isEditor) return;
            Debug.Log("[FolderManager] Using Application.persistentDataPath; no folder picker is required.");
        }
        public bool IsParticipantIdReady => participantIdInput ? idSubmitted && IsValidParticipantId(participantIdInput.text) : IsValidParticipantId(participantId);

        void OnEnable() {
            if (Application.isPlaying) {
                saveToQuest = false;
                folderManager = FolderManager.Instance ? FolderManager.Instance : folderManager;
                if (folderManager) questFolderName = folderManager.rootFolderName;
                if (participantIdPanel) participantIdPanel.SetActive(true);
                if (participantIdInput) { participantIdInput.SetTextWithoutNotify(""); participantIdInput.interactable = true; }
            }

            if (participantIdInput) {
                participantIdInput.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
                participantIdInput.characterLimit = 64;
                participantIdInput.lineType = TMP_InputField.LineType.SingleLine;
                idSubmitted = false; // A prefilled field still requires Done / Submit confirmation.
                participantIdInput.onValueChanged.AddListener(IdEdited);
                participantIdInput.onEndEdit.AddListener(IdEnded);
                if (Application.isPlaying && showVirtualKeyboard) {
                    keyboard = participantIdInput.GetComponent<PlaygroundParticipantKeyboard>();
                    if (!keyboard) keyboard = participantIdInput.gameObject.AddComponent<PlaygroundParticipantKeyboard>();
                    keyboard.input = participantIdInput; keyboard.recording = this; keyboard.head = recordingCamera ? recordingCamera.transform : null;
                    participantIdInput.shouldHideSoftKeyboard = true;
                }
            }
        }
        void IdEnded(string value) { if (!keyboard || !keyboard.IsOpen) SetParticipantId(value); }
        void IdEdited(string value) { if (!IsRecording && !IsSaving) idSubmitted = false; }

        public static bool IsValidParticipantId(string value) { return FolderManager.IsValidId(value); }
        public string ResolveOutputFolder() {
            var manager = FolderManager.Instance ? FolderManager.Instance : folderManager;
            if (manager) return IsValidParticipantId(LockedParticipantId) ? Path.Combine(manager.LocalRoot, "Module" + moduleNumber, LockedParticipantId) : manager.LocalRoot;
            if (Application.platform == RuntimePlatform.Android && !Application.isEditor) {
                string root=Path.Combine(Application.persistentDataPath,"Autism - Videos & Audios");
                return IsValidParticipantId(LockedParticipantId) ? Path.Combine(root,"Module"+moduleNumber,LockedParticipantId) : root;
            }
            if (string.IsNullOrWhiteSpace(outputFolder)) throw new ArgumentException("Set an output folder name or absolute path.");
            if (Path.IsPathRooted(outputFolder)) return Path.GetFullPath(outputFolder);
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrEmpty(documents)) documents = Application.persistentDataPath;
            return Path.GetFullPath(Path.Combine(documents, outputFolder));
        }
        public void SetParticipantId(string value) {
            if (keyboard && keyboard.IsOpen) return;
            if (IsRecording || IsSaving || startTask != null && !startTask.IsCompleted) { SetStatus("ID is locked for the current recording."); return; }
            if (!IsValidParticipantId(value)) { SetStatus("Use 4-64 characters, including a letter and a number. No spaces or symbols."); return; }
            participantId = value;
            idSubmitted = true;
            if (participantIdInput) participantIdInput.SetTextWithoutNotify(value);
        }
        public void SubmitParticipantId() { if (participantIdInput) SetParticipantId(participantIdInput.text); }
        public Task<bool> StartSessionAsync() { return startTask != null && !startTask.IsCompleted ? startTask : (startTask = StartCore()); }
        async Task<bool> StartCore() {
            if (!recordModule) { SetStatus("Video recording disabled"); return true; }
            if (IsRecording) return true;


            string id = participantIdInput ? participantIdInput.text : participantId;
            if (!IsValidParticipantId(id)) { LastError = "Enter 4-64 letters/numbers including a letter and a number; no spaces."; SetStatus(LastError); return false; }
            if (!recordingCamera || !gameAudioListener || !gameAudioListener.enabled) { LastError = "Assign the player camera and its enabled AudioListener."; SetStatus(LastError); return false; }
            try {
                LastError = null; LockedParticipantId = id;
                folderManager = FolderManager.Instance ? FolderManager.Instance : folderManager;
                string folder = folderManager ? await folderManager.PrepareParticipantAsync(moduleNumber, id) : ResolveOutputFolder();
                if (folderManager) { questFolderName = folderManager.rootFolderName; questSubfolderName = folderManager.SessionRelativeFolder(moduleNumber, id); }
                Directory.CreateDirectory(folder);
                string requestedFilename = BuildFileName(id, DateTime.Now);
                string filename = requestedFilename.Replace(':', '-'); // Windows-compatible staging filename.
                if (participantIdInput) participantIdInput.interactable = false;
                var host = new GameObject("Module1 video recording session - saves across scene exit");
                run = host.AddComponent<PlaygroundRecordingRun>(); run.StatusChanged += SetStatus;
                run.ConfigureQuestStorage(null); // Final MP4 stays in the participant's local folder.
                SetStatus("Starting video + game audio...");
                await run.StartCaptureAsync(recordingCamera, gameAudioListener, folder, filename,
                    Mathf.Max(16, videoResolution.x / 2 * 2), Mathf.Max(16, videoResolution.y / 2 * 2), framesPerSecond);
                float firstFrameDeadline = Time.realtimeSinceStartup + 10f;
                while (run.IsRecording && run.LiveVideoFrames == 0 && Time.realtimeSinceStartup < firstFrameDeadline) await Task.Yield();
                if (!run.IsRecording || run.LiveVideoFrames == 0) throw new InvalidOperationException("The camera did not produce a recording frame. Check the camera, graphics API and native encoder logs.");
                if (keyboard) keyboard.Close();
                if (participantIdPanel) participantIdPanel.SetActive(false);
                Debug.Log("[ModuleStart] ID confirmed: " + id + "; Module" + moduleNumber + "; camera and game audio recording active.");
                return true;
            } catch (Exception ex) {
                LastError = "NatCorder could not start. Check the recording camera, output folder and native plugin setup. " + ex.Message;
                SetStatus(LastError); Debug.LogError("[ModuleStart] " + ex);
                if (run) {
                    var cleanup = run.StopAndSaveAsync("Start failed");
                    if (await Task.WhenAny(cleanup, Task.Delay(5000)) != cleanup) {
                        LastError += " Encoder shutdown is still pending. Return to entry and restart the app before retrying.";
                        SetStatus(LastError); run.StatusChanged -= SetStatus; run = null;
                    } else { run.StatusChanged -= SetStatus; Destroy(run.gameObject); run = null; }
                }
                if (participantIdInput) participantIdInput.interactable = true;
                return false;
            }
        }
        public async Task<string> StopAndSaveAsync(string reason) {
            if (startTask != null && !startTask.IsCompleted) await startTask;
            if (!run) return LastSavedPath;
            LastSavedPath = await run.StopAndSaveAsync(reason);
            LastQuestSavedPath = run.QuestSavedPath;
            LastError = run.Error;
            if (participantIdInput) participantIdInput.interactable = true;
            return LastSavedPath;
        }
        public bool HasUnsavedRecording => run && (run.IsRecording || run.IsSaving);
        public void RetryPendingQuestTransfers() { /* Retired: persistent videos remain on this device. */ }
        public async void StopAndSave() { await StopAndSaveAsync("Manual stop / leaving module"); }
        public async void RequestStopAndSave(string reason) { await StopAndSaveAsync(reason); }
        void SetStatus(string message) { if (recordingStatusText) recordingStatusText.text = message; Debug.Log("[M1 Playground][RecordingStatus] " + message); }
        [ContextMenu("Open Recording Folder")]
        public void OpenRecordingFolder() {
            try { string folder = ResolveOutputFolder(); Directory.CreateDirectory(folder);
#if UNITY_EDITOR
                UnityEditor.EditorUtility.RevealInFinder(folder);
#else
                Application.OpenURL(new Uri(folder + Path.DirectorySeparatorChar).AbsoluteUri);
#endif
            } catch (Exception ex) { Debug.LogError("[M1 Playground][RecordingFolder] " + ex.Message); }
        }
        void OnDisable() {
            if (participantIdInput) { participantIdInput.onValueChanged.RemoveListener(IdEdited); participantIdInput.onEndEdit.RemoveListener(IdEnded); }
            if (run || startTask != null && !startTask.IsCompleted) RequestStopAndSave("Module disabled / left early");
        }
        void OnDestroy() { if (run) run.StatusChanged -= SetStatus; }
    }
}
