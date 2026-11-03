using System;
using System.IO;
using UnityEngine;

namespace BitsPilani.Module1.Playground {
    // Inserts the clean prompt digitally; headset speaker sound is never used as the prompt recording.
    public static class PlaygroundQuestionAudio {
        public static long AppendPrompt(PlaygroundPcmWavWriter writer, AudioClip prompt, int rate, int channels) {
            if (!prompt) return 0;
            if (writer == null || rate <= 0 || channels <= 0) throw new ArgumentException("A valid WAV writer and audio format are required.");
            if (prompt.loadState != AudioDataLoadState.Loaded && !prompt.LoadAudioData()) throw new IOException("The question voiceover could not be loaded.");
            var source = new float[checked(prompt.samples * prompt.channels)];
            if (!prompt.GetData(source, 0)) throw new IOException("The question voiceover must use Decompress On Load so its audio can be included in the answer file.");
            long frames = (long)Math.Round((double)prompt.samples * rate / prompt.frequency);
            var output = new float[2048 * channels];
            for (long first = 0; first < frames; first += 2048) {
                int count = (int)Math.Min(2048, frames - first);
                for (int frame = 0; frame < count; frame++) {
                    double position = (first + frame) * (double)prompt.frequency / rate;
                    int a = Math.Min((int)position, prompt.samples - 1), b = Math.Min(a + 1, prompt.samples - 1);
                    float fraction = (float)(position - a);
                    for (int ch = 0; ch < channels; ch++) {
                        float left = 0, right = 0;
                        if (prompt.channels == channels) { left = source[a * prompt.channels + ch]; right = source[b * prompt.channels + ch]; }
                        else {
                            for (int c = 0; c < prompt.channels; c++) { left += source[a * prompt.channels + c]; right += source[b * prompt.channels + c]; }
                            left /= prompt.channels; right /= prompt.channels;
                        }
                        output[frame * channels + ch] = Mathf.Lerp(left, right, fraction);
                    }
                }
                writer.Append(output, count * channels);
            }
            // A short pause separates the spoken question from the participant's answer.
            Array.Clear(output, 0, output.Length);
            int silence = (int)Math.Round(rate * .3);
            while (silence > 0) { int count = Math.Min(2048, silence); writer.Append(output, count * channels); silence -= count; }
            return writer.Frames;
        }
    }
}
