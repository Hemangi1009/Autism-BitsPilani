using System;
using System.IO;
using System.Text;

namespace BitsPilani.Module1.Playground {
    // Writes PCM incrementally instead of retaining the whole answer in managed memory.
    public sealed class PlaygroundPcmWavWriter : IDisposable {
        readonly FileStream stream;
        readonly BinaryWriter header;
        readonly int sampleRate, channels;
        byte[] bytes = new byte[4096];
        long dataBytes;
        bool disposed;
        public long Frames => dataBytes / (channels * 2);
        public float Seconds => (float)Frames / sampleRate;
        public PlaygroundPcmWavWriter(string path, int rate, int channelCount) {
            if (rate <= 0 || channelCount <= 0 || channelCount > 8) throw new ArgumentOutOfRangeException("Invalid audio format.");
            sampleRate = rate; channels = channelCount;
            stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
            header = new BinaryWriter(stream, Encoding.ASCII, true);
            try { WriteHeader(); } catch { try { header.Dispose(); } finally { stream.Dispose(); } throw; }
        }
        public void Append(float[] samples, int count) {
            if (disposed) throw new ObjectDisposedException(nameof(PlaygroundPcmWavWriter));
            if (samples == null || count < 0 || count > samples.Length || count % channels != 0) throw new ArgumentException("Samples must contain complete audio frames.");
            if (dataBytes + count * 2L > uint.MaxValue - 36) throw new IOException("The WAV file has reached its supported size limit. Stop this clip and start another.");
            int required = checked(count * 2); if (bytes.Length < required) bytes = new byte[required];
            for (int i = 0; i < count; i++) {
                float value = samples[i];
                if (float.IsNaN(value) || float.IsInfinity(value)) value = 0;
                value = Math.Max(-1f, Math.Min(1f, value));
                short pcm = (short)Math.Round(value < 0 ? value * 32768f : value * 32767f);
                bytes[i * 2] = (byte)(pcm & 255); bytes[i * 2 + 1] = (byte)((pcm >> 8) & 255);
            }
            stream.Position = 44 + dataBytes; stream.Write(bytes, 0, required); dataBytes += required;
        }
        void WriteHeader() {
            stream.Position = 0;
            header.Write(Encoding.ASCII.GetBytes("RIFF")); header.Write((uint)(36 + dataBytes));
            header.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); header.Write(16u); header.Write((ushort)1);
            header.Write((ushort)channels); header.Write((uint)sampleRate); header.Write((uint)(sampleRate * channels * 2));
            header.Write((ushort)(channels * 2)); header.Write((ushort)16);
            header.Write(Encoding.ASCII.GetBytes("data")); header.Write((uint)dataBytes); header.Flush();
            stream.Position = 44 + dataBytes;
        }
        public void Checkpoint() { if (disposed) return; WriteHeader(); stream.Flush(); }
        public void Dispose() {
            if (disposed) return;
            try { WriteHeader(); stream.Flush(true); }
            finally { disposed = true; try { header.Dispose(); } finally { stream.Dispose(); } }
        }
    }
}
