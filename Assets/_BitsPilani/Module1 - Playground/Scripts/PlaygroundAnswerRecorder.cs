using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BitsPilani.Module1.Playground {
    [Serializable] public sealed class PlaygroundAnswerClip {
        public int question, clip;
        public string fileName, path, startedUtc, endedUtc, stopReason;
        public float seconds;
        public long frames;
    }

    public sealed class PlaygroundAnswerRecorder : MonoBehaviour {
        public static PlaygroundAnswerRecorder Active { get; private set; }
        [Tooltip("Leave empty to use the default microphone (the headset microphone in a Quest APK).")]
        public string microphoneDevice;
        [Range(8000, 48000)] public int sampleRate = 16000;
        [Range(10, 60)] public int microphoneBufferSeconds = 30;
        [NonSerialized] public IPlaygroundMicrophoneSource MicrophoneSource;
        public bool IsRecording { get; private set; }
        public bool IsSaving { get; private set; }
        public string LastError { get; private set; }
        public string LastSavedPath { get; private set; }
        public float RecordedSeconds => wav == null ? 0 : wav.Seconds;
        public IReadOnlyList<PlaygroundAnswerClip> SavedClips => saved;
        public event Action<PlaygroundAnswerClip> ClipSaved;
        public event Action StateChanged;
        readonly List<PlaygroundAnswerClip> saved = new List<PlaygroundAnswerClip>();
        AudioClip microphoneClip;
        PlaygroundPcmWavWriter wav;
        PlaygroundAnswerClip current;
        string device, pendingPath, finalPath;
        float[] samples;
        int lastFrame;
        float lastRead, startedAt, checkpointAt;
        public int SavedCount(int question) { int count = 0; foreach (var clip in saved) if (clip.question == question) count++; return count; }
        public void ClearStatus() { if (!IsRecording && !IsSaving) LastError = null; }
        void NotifyState() { try { StateChanged?.Invoke(); } catch (Exception ex) { Debug.LogWarning("[M1 Audio][UIUpdate] " + ex.Message); } }

        public bool BeginAnswer(int question, string folder) {
            if (IsRecording || IsSaving) return false;
            try {
                if (Active && Active != this) throw new InvalidOperationException("Another microphone recording is active.");
                if (question < 1 || question > 3 || string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("A reflection question and participant recording folder are required.");
                MicrophoneSource = MicrophoneSource ?? new PlaygroundMicrophoneSource();
                if (!MicrophoneSource.HasPermission) throw new InvalidOperationException("Microphone permission is not granted. Enable it in the Quest app permissions, then try again.");
                var devices = MicrophoneSource.Devices;
                if (devices == null || devices.Length == 0) throw new InvalidOperationException("No microphone was found. Check the headset/PC microphone and try again.");
                device = string.IsNullOrWhiteSpace(microphoneDevice) ? null : microphoneDevice;
                if (device != null && Array.IndexOf(devices, device) < 0) throw new InvalidOperationException("The selected microphone is unavailable: " + device);
                int rate = Mathf.Clamp(sampleRate, 8000, 48000);
                MicrophoneSource.GetCaps(device, out int minimum, out int maximum);
                if (minimum > 0 && maximum >= minimum) rate = Mathf.Clamp(rate, minimum, maximum);
                Directory.CreateDirectory(folder);
                DateTime time = DateTime.Now;
                int number = SavedCount(question) + 1;
                string stem = "Audio_" + time.ToString("yyyy-MM-dd_HH-mm-ss") + "_Q" + question + "_Clip" + number.ToString("00");
                finalPath = Path.Combine(folder, stem + ".wav");
                for (int i = 2; File.Exists(finalPath); i++) finalPath = Path.Combine(folder, stem + "_" + i + ".wav");
                string staging = Path.Combine(folder, ".inprogress", "audio-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(staging); pendingPath = Path.Combine(staging, Path.GetFileName(finalPath) + ".partial");
                microphoneClip = MicrophoneSource.Start(device, Mathf.Clamp(microphoneBufferSeconds, 10, 60), rate);
                if (!microphoneClip || microphoneClip.channels < 1 || microphoneClip.samples < 1) throw new IOException("The microphone could not start. Check its permission and device settings.");
                wav = new PlaygroundPcmWavWriter(pendingPath, microphoneClip.frequency, microphoneClip.channels);
                samples = new float[Mathf.Max(256, microphoneClip.frequency / 10) * microphoneClip.channels];
                current = new PlaygroundAnswerClip { question = question, clip = number, startedUtc = time.ToUniversalTime().ToString("o") };
                lastFrame = 0; startedAt = lastRead = checkpointAt = Time.realtimeSinceStartup;
                LastError = null; IsRecording = true; Active = this;
                Debug.Log("[M1 Audio][RecordingStarted] Q" + question + " clip " + number + "; " + finalPath);
                NotifyState(); return true;
            } catch (Exception ex) {
                IsRecording = false; if (Active == this) Active = null;
                LastError = ex.Message; ReleaseMicrophone();
                try { wav?.Dispose(); } catch (Exception close) { Debug.LogWarning("[M1 Audio][CloseFailed] " + close.Message); }
                wav = null; samples = null;
                Debug.LogWarning("[M1 Audio][StartFailed] " + LastError); NotifyState(); return false;
            }
        }

        void DrainMicrophone() {
            if (!microphoneClip || wav == null) return;
            int position = MicrophoneSource.Position(device);
            if (position < 0 || position >= microphoneClip.samples) return;
            // Some platforms reset the cursor to zero as soon as the OS stops the mic.
            // That is not a ring-buffer wrap and must not append unwritten silence.
            if (position == 0 && !MicrophoneSource.IsRecording(device)) return;
            int available = (position - lastFrame + microphoneClip.samples) % microphoneClip.samples;
            int channels = microphoneClip.channels;
            while (available > 0) {
                int frames = Mathf.Min(available, samples.Length / channels);
                if (!microphoneClip.GetData(samples, lastFrame)) throw new IOException("The microphone audio samples could not be read.");
                wav.Append(samples, frames * channels);
                lastFrame = (lastFrame + frames) % microphoneClip.samples; available -= frames;
            }
            lastRead = Time.realtimeSinceStartup;
        }
        void Update() {
            if (!IsRecording || Time.realtimeSinceStartup - lastRead < .1f) return;
            try {
                float now = Time.realtimeSinceStartup;
                float bufferDuration = (float)microphoneClip.samples / microphoneClip.frequency;
                if (now - lastRead >= bufferDuration) throw new IOException("Microphone capture was interrupted for longer than its buffer. The recorded portion will be saved; record another clip to continue.");
                DrainMicrophone();
                if (wav.Frames == 0 && now - startedAt > 5f) throw new IOException("The microphone started but returned no audio. Check microphone access and try again.");
                if (!MicrophoneSource.IsRecording(device) && wav.Frames > 0) { StopAndSave("Microphone stopped"); return; }
                if (now - checkpointAt >= 1f) { wav.Checkpoint(); checkpointAt = now; }
            } catch (Exception ex) { StopAndSave("Capture interrupted", ex.Message); }
        }
        public PlaygroundAnswerClip StopAndSave(string reason = "User stopped recording", string captureError = null) {
            if (!IsRecording || IsSaving) return null;
            IsRecording = false; IsSaving = true; if (Active == this) Active = null; NotifyState();
            try {
                try { DrainMicrophone(); }
                catch (Exception ex) { if (wav == null || wav.Frames == 0) throw; captureError = "Saved the recorded portion; final microphone samples were unavailable. " + ex.Message; }
                finally { ReleaseMicrophone(); }
                if (wav == null || wav.Frames == 0) throw new IOException("No audio was captured. Select Record answer and speak before stopping.");
                current.frames = wav.Frames; current.seconds = wav.Seconds; current.endedUtc = DateTime.UtcNow.ToString("o"); current.stopReason = reason + (string.IsNullOrEmpty(captureError) ? "" : "; " + captureError);
                wav.Dispose(); wav = null;
                // Never overwrite an older recording, even if multiple clips begin in the same second.
                string target = finalPath, stem = Path.GetFileNameWithoutExtension(finalPath), folder = Path.GetDirectoryName(finalPath);
                for (int i = 2; File.Exists(target); i++) target = Path.Combine(folder, stem + "_" + i + ".wav");
                File.Move(pendingPath, target);
                current.path = LastSavedPath = target; current.fileName = Path.GetFileName(target);
                LastError = captureError; saved.Add(current);
                var finished = current; current = null;
                string staging = Path.GetDirectoryName(pendingPath);
                if (Directory.Exists(staging) && Directory.GetFileSystemEntries(staging).Length == 0) Directory.Delete(staging);
                Debug.Log("[M1 Audio][RecordingSaved] Q" + finished.question + " clip " + finished.clip + "; " + finished.seconds.ToString("F2") + "s; reason=" + reason + "; " + target);
                try { ClipSaved?.Invoke(finished); } catch (Exception ex) { Debug.LogWarning("[M1 Audio][SavedNotification] " + ex.Message); }
                return finished;
            } catch (Exception ex) {
                LastError = ex.Message;
                Debug.LogWarning("[M1 Audio][SaveFailed] " + LastError + "; unfinished audio retained at " + pendingPath); return null;
            } finally {
                ReleaseMicrophone();
                try { wav?.Dispose(); } catch (Exception ex) { Debug.LogWarning("[M1 Audio][CloseFailed] " + ex.Message); }
                wav = null; samples = null; IsSaving = false; NotifyState();
            }
        }
        void ReleaseMicrophone() {
            if (microphoneClip) {
                try { MicrophoneSource?.End(device); } catch (Exception ex) { Debug.LogWarning("[M1 Audio][MicrophoneStop] " + ex.Message); }
                if (Application.isPlaying) Destroy(microphoneClip); else DestroyImmediate(microphoneClip);
                microphoneClip = null;
            }
        }
        void OnApplicationPause(bool paused) { if (paused) StopAndSave("Application paused / left early"); }
        void OnApplicationQuit() { StopAndSave("Application quit"); }
        void OnDisable() { StopAndSave("Recorder disabled / scene left"); }
        void OnDestroy() { StopAndSave("Recorder destroyed"); if (Active == this) Active = null; }
    }
}
