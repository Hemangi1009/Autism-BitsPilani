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
        [Header("Output folder")]
        [Tooltip("An absolute folder path, or a folder name under Documents on desktop. Created if missing; existing folders are reused.")]
        public string outputFolder = "BitsPilaniRecordings";
        [Header("Quest headset destination")]
        [Tooltip("APK: publish to the headset media library. Quest Link: transfer the finished PC recording to the USB-connected Quest.")]
        public bool saveToQuest;
        public string questFolderName = "Quest";
        [Tooltip("Created inside the internal-storage Quest folder, or reused if it already exists.")]
        public string questSubfolderName = "bitspilani";
        [Tooltip("Retain a PC/app-private duplicate after successful headset publication. Failed/pending transfers always retain the source.")]
        public bool keepLocalCopyAfterQuestSave;
        [Tooltip("For Quest Link on PC only: Android SDK/platform-tools/adb.exe. Not used by an APK running on Quest.")]
        public string adbExecutable;
        [Tooltip("Optional. Set this when multiple Android devices are connected.")]
        public string questDeviceSerial;
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
        public string filenameFormat = "{id}_Module1_{timestamp}";
        public string timestampFormat = "yyyyMMdd_HHmmss_fff";
        [Header("Video quality")]
        public Vector2Int videoResolution = new Vector2Int(1280, 720);
        [Range(10, 60)] public int framesPerSecond = 30;
        public string LastError { get; private set; }
        public string LastSavedPath { get; private set; }
        public string LastQuestSavedPath { get; private set; }
        public bool QuestTransferPending => saveToQuest && run && run.QuestTransferPending;
        public string LockedParticipantId { get; private set; }
        public bool IsRecording => run && run.IsRecording;
        public bool IsSaving => run && run.IsSaving;
        PlaygroundRecordingRun run;
        Task<bool> startTask;
        bool idSubmitted;
        bool retryingQuestTransfers;
        PlaygroundParticipantKeyboard keyboard;
        PlaygroundQuestFolderAccess folderAccess;
        public bool IsQuestFolderReady => !saveToQuest || Application.platform != RuntimePlatform.Android || Application.isEditor || folderAccess && folderAccess.Ready;
        public string FileNamePreview { get { try { return BuildFileName(IsValidParticipantId(participantId) ? participantId : "ID", DateTime.Now) + ".mp4"; } catch (Exception ex) { return ex.Message; } } }
        public string BuildFileName(string id, DateTime time) {
            if (string.IsNullOrEmpty(filenameFormat) || !filenameFormat.Contains("{id}") || !filenameFormat.Contains("{timestamp}")) throw new ArgumentException("Filename Format must contain {id} and {timestamp}.");
            string name = filenameFormat.Replace("{id}", id).Replace("{timestamp}", time.ToString(timestampFormat));
            if (name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4);
            if (name.Length > 180 || !Regex.IsMatch(name, "\\A[A-Za-z0-9_.-]+\\z")) throw new ArgumentException("Filename format can use letters, numbers, underscore, hyphen and dot only.");
            return name;
        }
        public void SelectQuestFolder() {
            if (Application.platform != RuntimePlatform.Android || Application.isEditor) return;
            folderAccess = PlaygroundQuestFolderAccess.Get(questFolderName); folderAccess.Choose();
        }
        public bool IsParticipantIdReady => participantIdInput ? idSubmitted && IsValidParticipantId(participantIdInput.text) : IsValidParticipantId(participantId);

        void OnEnable() {
            if (Application.isPlaying && saveToQuest && Application.platform == RuntimePlatform.Android && !Application.isEditor) folderAccess = PlaygroundQuestFolderAccess.Get(questFolderName);
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

        public static bool IsValidParticipantId(string value) { return !string.IsNullOrEmpty(value) && Regex.IsMatch(value, "\\A[A-Za-z0-9]{1,64}\\z"); }
        public string ResolveOutputFolder() {
            if (saveToQuest && Application.platform == RuntimePlatform.Android && !Application.isEditor) return Path.Combine(Application.persistentDataPath, "RecordingStaging");
            if (string.IsNullOrWhiteSpace(outputFolder)) throw new ArgumentException("Set an output folder name or absolute path.");
            if (Path.IsPathRooted(outputFolder)) return Path.GetFullPath(outputFolder);
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrEmpty(documents)) documents = Application.persistentDataPath;
            return Path.GetFullPath(Path.Combine(documents, outputFolder));
        }
        public void SetParticipantId(string value) {
            if (keyboard && keyboard.IsOpen) return;
            if (IsRecording || IsSaving || startTask != null && !startTask.IsCompleted) { SetStatus("ID is locked for the current recording."); return; }
            if (!IsValidParticipantId(value)) { SetStatus("Enter 1-64 letters or numbers; spaces and symbols are not accepted."); return; }
            participantId = value;
            idSubmitted = true;
            if (participantIdInput) participantIdInput.SetTextWithoutNotify(value);
        }
        public void SubmitParticipantId() { if (participantIdInput) SetParticipantId(participantIdInput.text); }
        public Task<bool> StartSessionAsync() { return startTask != null && !startTask.IsCompleted ? startTask : (startTask = StartCore()); }
        async Task<bool> StartCore() {
            if (!recordModule) { SetStatus("Video recording disabled"); return true; }
            if (IsRecording) return true;
            if (!IsQuestFolderReady) { LastError = "Choose the internal-storage Quest folder before recording."; SetStatus(LastError); return false; }
            string id = participantIdInput ? participantIdInput.text : participantId;
            if (!IsValidParticipantId(id)) { LastError = "Enter a participant ID using letters and numbers only (1-64 characters)."; SetStatus(LastError); return false; }
            if (!recordingCamera || !gameAudioListener || !gameAudioListener.enabled) { LastError = "Assign the player camera and its enabled AudioListener."; SetStatus(LastError); return false; }
            try {
                LastError = null; LockedParticipantId = id;
                string folder = ResolveOutputFolder();
                Directory.CreateDirectory(folder);
                string filename = BuildFileName(id, DateTime.Now);
                if (participantIdInput) participantIdInput.interactable = false;
                var host = new GameObject("Module1 video recording session - saves across scene exit");
                run = host.AddComponent<PlaygroundRecordingRun>(); run.StatusChanged += SetStatus;
                run.ConfigureQuestStorage(new PlaygroundQuestStorageSettings {
                    enabled = saveToQuest, android = Application.platform == RuntimePlatform.Android && !Application.isEditor,
                    folderName = questFolderName, subfolderName = questSubfolderName, adbExecutable = adbExecutable,
                    deviceSerial = questDeviceSerial, keepLocalCopy = keepLocalCopyAfterQuestSave
                });
                SetStatus("Starting video + game audio...");
                await run.StartCaptureAsync(recordingCamera, gameAudioListener, folder, filename,
                    Mathf.Max(16, videoResolution.x / 2 * 2), Mathf.Max(16, videoResolution.y / 2 * 2), framesPerSecond);
                return run.IsRecording;
            } catch (Exception ex) {
                LastError = "NatCorder could not start. Check the recording camera, output folder and native plugin setup. " + ex.Message;
                SetStatus(LastError);
                if (run) { await run.StopAndSaveAsync("Start failed"); run.StatusChanged -= SetStatus; Destroy(run.gameObject); run = null; }
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
        [ContextMenu("Send Pending Videos to Quest")]
        public async void RetryPendingQuestTransfers() {
            if (!saveToQuest || retryingQuestTransfers) return;
            retryingQuestTransfers = true;
            try {
                int count = await PlaygroundQuestVideoStorage.RetryPendingAsync(ResolveOutputFolder());
                if (count > 0) SetStatus(count + " pending video(s) saved to headset " + questFolderName + "/" + questSubfolderName + ".");
            } catch (Exception ex) { SetStatus("Quest transfer pending: " + ex.Message); }
            finally { retryingQuestTransfers = false; }
        }
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
