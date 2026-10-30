using System.Threading;
using NatSuite.Recorders;
using NatSuite.Recorders.Clocks;
using UnityEngine;

namespace BitsPilani.Module1.Playground {
    public sealed class PlaygroundGameAudioCapture : MonoBehaviour {
        readonly object sync = new object();
        IMediaRecorder recorder;
        IClock clock;
        long buffers;
        float peak;
        bool androidAudioThread;
        public long BufferCount => Interlocked.Read(ref buffers);
        public float Peak => peak;
        public void Initialize(IMediaRecorder target, IClock sessionClock) {
            lock (sync) { recorder = target; clock = sessionClock; androidAudioThread = Application.platform == RuntimePlatform.Android && !Application.isEditor; }
        }
        void OnAudioFilterRead(float[] data, int channels) {
            lock (sync) {
                if (recorder == null) return;
                // NatCorder's Android encoder uses JNI; the audio callback is not the Unity main thread.
                if (androidAudioThread) AndroidJNI.AttachCurrentThread();
                recorder.CommitSamples(data, clock.timestamp);
                Interlocked.Increment(ref buffers);
                for (int i = 0; i < data.Length; i++) { float value = data[i] < 0 ? -data[i] : data[i]; if (value > peak) peak = value; }
            }
        }
        public void StopCapture() { lock (sync) { recorder = null; clock = null; } }
        void OnDisable() { StopCapture(); }
        void OnDestroy() { StopCapture(); }
    }
}
