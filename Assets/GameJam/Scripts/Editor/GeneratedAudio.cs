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
