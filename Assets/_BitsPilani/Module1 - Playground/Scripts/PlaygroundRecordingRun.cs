using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using NatSuite.Recorders;
using NatSuite.Recorders.Clocks;

namespace BitsPilani.Module1.Playground {
    // Remains alive across scene unload until the MP4 has been finalized and moved.
    public sealed class PlaygroundRecordingRun : MonoBehaviour {
        public static PlaygroundRecordingRun Active { get; private set; }
        public bool IsRecording { get; private set; }
        public bool IsSaving { get; private set; }
        public string SavedPath { get; private set; }
        public string Error { get; private set; }
        public int VideoFrames { get; private set; }
        public long AudioSamples => audio ? audio.BufferCount : audioSamples;
        public float AudioPeak { get; private set; }
        IMediaRecorder recorder;
        PlaygroundCameraCapture capture;
        PlaygroundGameAudioCapture audio;
        string finalPath, pendingDirectory;
        Task startTask;
        Task<string> finishTask;
        Task<string> nativeFinishTask;
        bool stopRequested, allowQuit, quitPending;
        long audioSamples;
        public event Action<string> StatusChanged;

        void Awake() { DontDestroyOnLoad(gameObject); Application.wantsToQuit += WantsToQuit; }
        public Task StartCaptureAsync(Camera camera, AudioListener listener, string directory, string filename, int width, int height, int frameRate) {
            if (Active && Active != this) throw new InvalidOperationException("Another module recording is still active or saving.");
            Active = this;
            return startTask ?? (startTask = StartCore(camera, listener, directory, filename, width, height, frameRate));
        }
        Task StartCore(Camera camera, AudioListener listener, string directory, string filename, int width, int height, int frameRate) {
            try {
                Directory.CreateDirectory(directory);
                finalPath = Path.Combine(directory, filename + ".mp4");
                for (int i = 2; File.Exists(finalPath); i++) finalPath = Path.Combine(directory, filename + "_" + i + ".mp4");
                pendingDirectory = Path.Combine(directory, ".inprogress", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(pendingDirectory);
                recorder = new PlaygroundNatCorderRecorder(Path.Combine(pendingDirectory, "capture.mp4"), width, height, frameRate,
                    AudioSettings.outputSampleRate, ChannelCount(AudioSettings.speakerMode));
                if (stopRequested) return Task.CompletedTask;
                var clock = new RealtimeClock();
                var cameraObject = new GameObject("Module1 mono recording camera"); cameraObject.transform.SetParent(transform, false);
                capture = cameraObject.AddComponent<PlaygroundCameraCapture>();
                audio = listener.gameObject.AddComponent<PlaygroundGameAudioCapture>();
                // Start the audio clock with the first rendered video frame, rather than
                // during camera/shader setup, to avoid an audio lead at the beginning.
                capture.FirstFrameCaptured = () => { if (audio) audio.Initialize(recorder, clock); };
                capture.Initialize(camera, recorder, clock, width, height, frameRate);
                IsRecording = true;
                StatusChanged?.Invoke("Recording video + game audio");
                Debug.Log("[M1 Playground][RecordingStarted][NatCorder] " + finalPath);
                return Task.CompletedTask;
            } catch (Exception ex) {
                Error = ex.Message; StopInputs();
                StatusChanged?.Invoke("Recording could not start: " + Error);
                Debug.LogError("[M1 Playground][RecordingStartFailed] " + Error);
                throw;
            }
        }
        static int ChannelCount(AudioSpeakerMode mode) {
            switch (mode) { case AudioSpeakerMode.Mono: return 1; case AudioSpeakerMode.Quad: return 4; case AudioSpeakerMode.Surround: return 5; case AudioSpeakerMode.Mode5point1: return 6; case AudioSpeakerMode.Mode7point1: return 8; default: return 2; }
        }
        public Task<string> StopAndSaveAsync(string reason) {
            stopRequested = true;
            return finishTask ?? (finishTask = FinishCore(reason));
        }
        async Task<string> FinishCore(string reason) {
            IsSaving = true; IsRecording = false;
            try {
                if (startTask != null) { try { await startTask; } catch { } }
                StopInputs();
                if (recorder == null) return null;
                StatusChanged?.Invoke("Saving video...");
                var asset = await (nativeFinishTask ?? (nativeFinishTask = recorder.FinishWriting()));
                SaveNativeAsset(asset);
                StatusChanged?.Invoke("Video saved: " + SavedPath);
                Debug.Log("[M1 Playground][RecordingSaved][NatCorder] reason=" + reason + "; videoFrames=" + VideoFrames + "; audioBuffers=" + AudioSamples + "; audioPeak=" + AudioPeak + "; " + SavedPath);
                return SavedPath;
            } catch (Exception ex) {
                Error = ex.Message; StatusChanged?.Invoke("Video save failed: " + Error);
                Debug.LogError("[M1 Playground][RecordingSaveFailed] " + Error + "; pending files preserved at " + pendingDirectory);
                return null;
            } finally {
                IsSaving = false;
                if (Active == this) Active = null;
            }
        }
        void StopInputs() {
            if (audio) { audio.StopCapture(); audioSamples = audio.BufferCount; AudioPeak = audio.Peak; Destroy(audio); audio = null; }
            if (capture) { VideoFrames = capture.FramesWritten; capture.Dispose(); }
        }
        void SaveNativeAsset(string path) {
            if (!string.IsNullOrEmpty(SavedPath)) return;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new IOException("NatCorder did not return a saved video file.");
            if (!string.Equals(Path.GetFullPath(path), Path.GetFullPath(finalPath), StringComparison.OrdinalIgnoreCase)) {
                try { File.Move(path, finalPath); }
                catch (IOException) { File.Copy(path, finalPath, false); } // Some native readers retain a Windows file handle; preserve that source.
            }
            SavedPath = finalPath; recorder = null;
            if (Directory.Exists(pendingDirectory) && Directory.GetFileSystemEntries(pendingDirectory).Length == 0) Directory.Delete(pendingDirectory);
        }
#if UNITY_EDITOR
        // Editor Stop unloads the managed domain, so an async continuation alone is insufficient.
        // Finish native writing before that unload; no Unity synchronization-context await here.
        public void SaveBeforeEditorStops() {
            if (!string.IsNullOrEmpty(SavedPath) || recorder == null) return;
            stopRequested = true; IsRecording = false; IsSaving = true; StopInputs();
            try {
                nativeFinishTask = nativeFinishTask ?? recorder.FinishWriting();
                if (!nativeFinishTask.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Timed out finalizing video before Editor Stop. Use Stop and Save before stopping Play Mode.");
                SaveNativeAsset(nativeFinishTask.GetAwaiter().GetResult());
                Debug.Log("[M1 Playground][RecordingSaved] Editor Stop / reload: " + SavedPath);
            } catch (Exception ex) { Error = ex.Message; Debug.LogError("[M1 Playground][EditorRecordingSave] " + Error + "; pending files: " + pendingDirectory); }
            finally { IsSaving = false; if (Active == this) Active = null; }
        }
#endif
        public async void RequestStop(string reason) { await StopAndSaveAsync(reason); }
        bool WantsToQuit() {
            if (allowQuit || (!IsRecording && !IsSaving && (startTask == null || startTask.IsCompleted))) return true;
            if (!quitPending) { quitPending = true; SaveThenQuit(); }
            return false;
        }
        async void SaveThenQuit() { await StopAndSaveAsync("Application quit"); allowQuit = true; Application.Quit(); }
        void OnApplicationPause(bool paused) { if (paused && IsRecording) RequestStop("Application paused / left early"); }
        void OnApplicationQuit() { if (IsRecording || IsSaving) RequestStop("Application quitting"); }
        void OnDestroy() { Application.wantsToQuit -= WantsToQuit; if (IsRecording) RequestStop("Recording host destroyed"); if (Active == this) Active = null; }
    }
}
