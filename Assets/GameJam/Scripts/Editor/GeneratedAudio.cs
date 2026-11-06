using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameJam.Editor
{
    /// <summary>
    /// Placeholder sounds synthesised in code and saved as WAVs under Audio/Generated (no third-party clips).
    /// Hand taps are short soft thumps; the slide is a seamless loop of filtered noise. Replace with real foley later
    /// by assigning clips on the rig's Sounds component.
    /// </summary>
    public static class GeneratedAudio
    {
        public const string Folder = "Assets/GameJam/Audio/Generated";
        private const int SampleRate = 22050;

        /// <summary>A short palm-on-surface thump; pitch varies per variant so repeated taps don't sound robotic.</summary>
        public static AudioClip Tap(string name, float pitchHz, int seed) =>
            Create(name, 0.09f, false, seed, (t, noise) =>
            {
                float envelope = Mathf.Exp(-t * 55f);
                float body = Mathf.Sin(2f * Mathf.PI * pitchHz * t * (1f - t * 2.5f));
                float click = noise * Mathf.Exp(-t * 400f);
                return (body * 0.75f + click * 0.35f) * envelope;
            });

        // A stretched round membrane rings at these inharmonic ratios (Bessel-function modes), which is what makes a
        // balloon (or a drum) sound hollow rather than tonal.
        private static readonly float[] MembraneRatios = { 1f, 1.59f, 2.14f, 2.30f, 2.65f, 2.92f };
        private static readonly float[] MembraneLevels = { 1f, 0.55f, 0.42f, 0.3f, 0.22f, 0.15f };
        private static readonly float[] MembraneDecay = { 16f, 24f, 30f, 36f, 44f, 52f };

        /// <summary>
        /// A hand slapping a party balloon: a bright skin-on-latex crack, a dull puff of air, and the hollow ring of the
        /// stretched rubber dying away fast. Variants shift the size of the balloon a little so repeats don't sound identical.
        /// </summary>
        public static AudioClip BalloonSlap(string name, int variant)
        {
            string path = $"{Folder}/{name}.wav";
            var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (existing != null)
            {
                return existing;
            }

            float baseHz = 105f + variant * 14f;
            int count = Mathf.RoundToInt(0.26f * SampleRate);
            var samples = new float[count];
            var random = new System.Random(31 + variant);
            var phases = new float[MembraneRatios.Length];
            for (int k = 0; k < phases.Length; k++)
            {
                phases[k] = (float)(random.NextDouble() * Math.PI * 2.0);
            }

            float midNoise = 0f;
            float lowNoise = 0f;
            float peak = 0.0001f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                midNoise += (white - midNoise) * 0.35f;
                lowNoise += (white - lowNoise) * 0.08f;
                float crack = (white - midNoise) * Mathf.Exp(-t * 140f);       // bright, very short: the skin smack
                float puff = lowNoise * Mathf.Exp(-t * 35f) * 2.2f;          // dull air thup

                float membrane = 0f;
                float bend = 1f - 0.06f * t;                                  // the rubber relaxes a touch as it rings
                for (int k = 0; k < MembraneRatios.Length; k++)
                {
                    float hz = baseHz * MembraneRatios[k] * bend;
                    membrane += MembraneLevels[k] * Mathf.Sin(2f * Mathf.PI * hz * t + phases[k]) * Mathf.Exp(-MembraneDecay[k] * t);
                }

                float attack = Mathf.Clamp01(t * 1500f);
                samples[i] = (membrane * 0.55f + crack + puff) * attack;
                peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
            }

            for (int i = 0; i < count; i++)
            {
                samples[i] *= 0.85f / peak;
            }

            return Save(path, samples);
        }

        private static float Sine(float hz, float t) => Mathf.Sin(2f * Mathf.PI * hz * t);

        /// <summary>A bell-like note: fundamental plus a soft octave, ringing down.</summary>
        private static float Bell(float hz, float t, float decay) =>
            t < 0f ? 0f : (Sine(hz, t) + 0.35f * Sine(hz * 2f, t) + 0.12f * Sine(hz * 3f, t)) * Mathf.Exp(-t * decay) * Mathf.Clamp01(t * 300f);

        /// <summary>Hoop cleared: three bright bell notes rising (C6 E6 G6).</summary>
        public static AudioClip HoopChime(string name) =>
            Create(name, 1.1f, false, 21, (t, noise) =>
                (Bell(1047f, t, 5f) + Bell(1319f, t - 0.09f, 5f) + Bell(1568f, t - 0.18f, 4f)) * 0.3f);

        /// <summary>Run won: a short rising fanfare (C5 E5 G5 then a held C6 with a little vibrato).</summary>
        public static AudioClip WinFanfare(string name) =>
            Create(name, 2.2f, false, 22, (t, noise) =>
            {
                float s = Bell(523f, t, 4f) + Bell(659f, t - 0.16f, 4f) + Bell(784f, t - 0.32f, 4f);
                float held = t - 0.5f;
                if (held > 0f)
                {
                    float vibrato = 1f + 0.006f * Sine(5.5f, held);
                    s += (Sine(1047f * vibrato, held) + 0.4f * Sine(2094f * vibrato, held)) * Mathf.Exp(-held * 1.4f) * Mathf.Clamp01(held * 40f);
                }

                return s * 0.28f;
            });

        /// <summary>Trampoline: a springy boing (a pitch that drops and wobbles).</summary>
        public static AudioClip TrampolineBoing(string name) =>
            Create(name, 0.6f, false, 23, (t, noise) =>
            {
                float pitch = 140f + 260f * Mathf.Exp(-t * 6f) + 25f * Sine(14f, t) * Mathf.Exp(-t * 3f);
                return Sine(pitch, t) * Mathf.Exp(-t * 4.5f) * Mathf.Clamp01(t * 200f) * 0.8f;
            });

        /// <summary>Balloon pop: a sharp crack of air with a low thump under it.</summary>
        public static AudioClip BalloonPop(string name) =>
            Create(name, 0.35f, false, 24, (t, noise) =>
            {
                float crack = noise * 3f * Mathf.Exp(-t * 70f);
                float thump = Sine(85f * (1f - t), t) * Mathf.Exp(-t * 18f) * 0.7f;
                return Mathf.Clamp((crack + thump) * Mathf.Clamp01(t * 2000f), -1f, 1f) * 0.9f;
            });

        /// <summary>Bubble wand: a seamless loop of little bubbly pops at scattered times and pitches.</summary>
        public static AudioClip BubbleLoop(string name) =>
            Create(name, 2.2f, true, 25, (t, noise) =>
            {
                float s = 0f;
                for (int k = 0; k < 16; k++)
                {
                    float onset = Mathf.Repeat(k * 0.618034f, 1f) * 2f;
                    float local = t - onset;
                    if (local < 0f || local > 0.12f)
                    {
                        continue;
                    }

                    float baseHz = 420f + Mathf.Repeat(k * 0.381966f, 1f) * 520f;
                    s += Sine(baseHz * (1f + local * 9f), local) * Mathf.Exp(-local * 35f);
                }

                return s * 0.35f;
            });

        /// <summary>Wind: a seamless loop of soft rushing air that swells gently.</summary>
        public static AudioClip WindLoop(string name) =>
            Create(name, 3f, true, 26, (t, noise) => noise * (0.32f + 0.12f * Mathf.Sin(2f * Mathf.PI * t / 3f)));

        /// <summary>A gentle hiss for sliding hands; loops without a click (fades are crossfaded at the seam).</summary>
        public static AudioClip SlideLoop(string name) =>
            Create(name, 1f, true, 7, (t, noise) => noise * 0.18f);

        private static AudioClip Create(string name, float seconds, bool loop, int seed, Func<float, float, float> sample)
        {
            string path = $"{Folder}/{name}.wav";
            var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (existing != null)
            {
                return existing;
            }

            int count = Mathf.RoundToInt(seconds * SampleRate);
            var samples = new float[count];
            var random = new System.Random(seed);
            float lowPassed = 0f;
            for (int i = 0; i < count; i++)
            {
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                lowPassed += (white - lowPassed) * 0.25f; // soften the noise
                samples[i] = sample((float)i / SampleRate, lowPassed);
            }

            if (loop)
            {
                // Crossfade the last 10% into the start so the loop point is seamless.
                int fade = count / 10;
                for (int i = 0; i < fade; i++)
                {
                    float w = (float)i / fade;
                    samples[i] = samples[i] * w + samples[count - fade + i] * (1f - w);
                }

                Array.Resize(ref samples, count - fade);
            }

            return Save(path, samples);
        }

        private static AudioClip Save(string path, float[] samples)
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllBytes(path, ToWav(samples));
            AssetDatabase.ImportAsset(path);

            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono = true;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad; // tiny clips: no decode cost when played
            settings.compressionFormat = AudioCompressionFormat.ADPCM;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static byte[] ToWav(float[] samples)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            int dataBytes = samples.Length * 2;
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataBytes);
            writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write((short)1); // mono
            writer.Write(SampleRate);
            writer.Write(SampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataBytes);
            foreach (float sample in samples)
            {
                writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
            }

            writer.Flush();
            return stream.ToArray();
        }
    }
}
