// Loop seam and level check for the generated audio (Edit or Play mode). Reads the WAVs under Audio/Generated directly.
// Per loop: the jump from the last sample back to the first, against the clip's typical (95th percentile) sample-to-sample
// step; a seam is clean when the jump is no bigger than an ordinary step. Per clip: peak and RMS level (dBFS).
//   unity command script eval --project-path "D:\Unity\game-jam" -- --file .claude/unity-snippets/audio_loop_check.cs
using System;
using System.IO;
using System.Linq;
using UnityEngine;

string folder = "Assets/GameJam/Audio/Generated";
string[] loops = { "Item_BubbleLoop", "Wind_Loop", "Slide_Loop", "Music_Backyard" };
var sb = new System.Text.StringBuilder();
foreach (string file in Directory.GetFiles(folder, "*.wav").OrderBy(f => f))
{
    byte[] bytes = File.ReadAllBytes(file);
    int channels = BitConverter.ToInt16(bytes, 22);
    int dataStart = 44;
    int frames = (bytes.Length - dataStart) / 2 / channels;
    float[] mono = new float[frames];
    float peak = 0f;
    double sumSquares = 0;
    for (int i = 0; i < frames; i++)
    {
        float s = 0f;
        for (int c = 0; c < channels; c++)
        {
            float v = BitConverter.ToInt16(bytes, dataStart + (i * channels + c) * 2) / 32768f;
            peak = Mathf.Max(peak, Mathf.Abs(v));
            sumSquares += v * v;
            s += v / channels;
        }

        mono[i] = s;
    }

    string name = Path.GetFileNameWithoutExtension(file);
    float rms = Mathf.Sqrt((float)(sumSquares / (frames * channels)));
    sb.Append($"\n{name}: peak {20f * Mathf.Log10(peak + 1e-9f):F1} dB, rms {20f * Mathf.Log10(rms + 1e-9f):F1} dB");
    if (loops.Contains(name))
    {
        var steps = new float[frames - 1];
        for (int i = 1; i < frames; i++)
        {
            steps[i - 1] = Mathf.Abs(mono[i] - mono[i - 1]);
        }

        Array.Sort(steps);
        float typical = steps[(int)(steps.Length * 0.95f)];
        float seam = Mathf.Abs(mono[0] - mono[frames - 1]);
        sb.Append($", loop seam jump {seam:F4} vs typical step {typical:F4} -> {(seam <= typical * 1.5f ? "CLEAN" : "CLICK")}");
    }
}

return sb.ToString();
