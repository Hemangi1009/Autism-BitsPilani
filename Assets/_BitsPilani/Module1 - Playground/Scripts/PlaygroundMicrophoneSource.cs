using UnityEngine;

namespace BitsPilani.Module1.Playground {
    public interface IPlaygroundMicrophoneSource {
        bool HasPermission { get; }
        string[] Devices { get; }
        void GetCaps(string device, out int minimum, out int maximum);
        AudioClip Start(string device, int seconds, int sampleRate);
        int Position(string device);
        bool IsRecording(string device);
        void End(string device);
    }
    public sealed class PlaygroundMicrophoneSource : IPlaygroundMicrophoneSource {
        public bool HasPermission {
            get {
#if UNITY_ANDROID && !UNITY_EDITOR
                return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
#else
                return true; // Windows device/privacy availability is checked by Microphone.Start.
#endif
            }
        }
        public string[] Devices => Microphone.devices;
        public void GetCaps(string device, out int minimum, out int maximum) { Microphone.GetDeviceCaps(device, out minimum, out maximum); }
        public AudioClip Start(string device, int seconds, int sampleRate) { return Microphone.Start(device, true, seconds, sampleRate); }
        public int Position(string device) { return Microphone.GetPosition(device); }
        public bool IsRecording(string device) { return Microphone.IsRecording(device); }
        public void End(string device) { Microphone.End(device); }
    }
}
