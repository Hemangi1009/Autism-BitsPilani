using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace BitsPilani.Module1.Playground {
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
        [Header("Participant identity")]
        [Tooltip("Letters and numbers only, 1 to 64 characters. Used when Participant ID Input is unassigned.")]
        public string participantId = "Participant001";
        [Tooltip("Assign your canvas's TMP_InputField here. Its current text takes priority over the Inspector ID.")]
        public TMP_InputField participantIdInput;
        public TMP_Text recordingStatusText;
        [Header("Video quality")]
        public Vector2Int videoResolution = new Vector2Int(1280, 720);
        [Range(10, 60)] public int framesPerSecond = 30;
        public string LastError { get; private set; }
        public string LastSavedPath { get; private set; }
        public string LockedParticipantId { get; private set; }
        public bool IsRecording => run && run.IsRecording;
        public bool IsSaving => run && run.IsSaving;
        PlaygroundRecordingRun run;
        Task<bool> startTask;
        bool idSubmitted;
        public bool IsParticipantIdReady => participantIdInput ? idSubmitted && IsValidParticipantId(participantIdInput.text) : IsValidParticipantId(participantId);

        void OnEnable() {
            if (participantIdInput) {
                participantIdInput.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
                participantIdInput.characterLimit = 64;
                participantIdInput.lineType = TMP_InputField.LineType.SingleLine;
                idSubmitted = IsValidParticipantId(participantIdInput.text);
                participantIdInput.onValueChanged.AddListener(IdEdited);
                participantIdInput.onEndEdit.AddListener(SetParticipantId);
            }
        }
        void IdEdited(string value) { if (!IsRecording && !IsSaving) idSubmitted = false; }

        public static bool IsValidParticipantId(string value) { return !string.IsNullOrEmpty(value) && Regex.IsMatch(value, "\\A[A-Za-z0-9]{1,64}\\z"); }
        public string ResolveOutputFolder() {
            if (string.IsNullOrWhiteSpace(outputFolder)) throw new ArgumentException("Set an output folder name or absolute path.");
            if (Path.IsPathRooted(outputFolder)) return Path.GetFullPath(outputFolder);
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrEmpty(documents)) documents = Application.persistentDataPath;
            return Path.GetFullPath(Path.Combine(documents, outputFolder));
        }
        public void SetParticipantId(string value) {
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
            string id = participantIdInput ? participantIdInput.text : participantId;
            if (!IsValidParticipantId(id)) { LastError = "Enter a participant ID using letters and numbers only (1-64 characters)."; SetStatus(LastError); return false; }
            if (!recordingCamera || !gameAudioListener || !gameAudioListener.enabled) { LastError = "Assign the player camera and its enabled AudioListener."; SetStatus(LastError); return false; }
            try {
                LastError = null; LockedParticipantId = id;
                string folder = ResolveOutputFolder();
                Directory.CreateDirectory(folder);
                string filename = id + "_Module1_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                if (participantIdInput) participantIdInput.interactable = false;
                var host = new GameObject("Module1 video recording session - saves across scene exit");
                run = host.AddComponent<PlaygroundRecordingRun>(); run.StatusChanged += SetStatus;
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
            LastError = run.Error;
            if (participantIdInput) participantIdInput.interactable = true;
            return LastSavedPath;
        }
        public bool HasUnsavedRecording => run && (run.IsRecording || run.IsSaving);
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
            if (participantIdInput) { participantIdInput.onValueChanged.RemoveListener(IdEdited); participantIdInput.onEndEdit.RemoveListener(SetParticipantId); }
            if (run || startTask != null && !startTask.IsCompleted) RequestStopAndSave("Module disabled / left early");
        }
        void OnDestroy() { if (run) run.StatusChanged -= SetStatus; }
    }
}
