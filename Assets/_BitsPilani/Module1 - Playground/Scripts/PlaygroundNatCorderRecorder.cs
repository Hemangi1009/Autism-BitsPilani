using System;
using NatSuite.Recorders.Internal;

namespace BitsPilani.Module1.Playground {
    // The installed MP4Recorder chooses an internal default path. Use its same native
    // API and NativeRecorder implementation with our configured session output path.
    internal sealed class PlaygroundNatCorderRecorder : NativeRecorder {
        public PlaygroundNatCorderRecorder(string path, int width, int height, int fps, int sampleRate, int channels)
            : base(Create(path, width, height, fps, sampleRate, channels)) { }
        static IntPtr Create(string path, int width, int height, int fps, int sampleRate, int channels) {
            NatSuite.Recorders.Internal.NatCorder.CreateMP4Recorder(path, width, height, fps,
                sampleRate, channels, 8_000_000, 2, 128_000, out var handle);
            if (handle == IntPtr.Zero) throw new InvalidOperationException("NatCorder could not create the MP4 encoder. Check the output folder and recording format.");
            return handle;
        }
    }
}
