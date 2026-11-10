using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameJam.Editor
{
    /// <summary>
    /// Every sound in the game, synthesised in code and saved as WAVs under Audio/Generated (no third-party clips):
    /// hand taps, balloon slaps and pop, hoop chime, win fanfare, trampoline boing, the bubble, wind and slide loops, and
    /// the background music. Sounds are rendered at 44.1 kHz and normalised to a level per kind, so the mix starts out
    /// balanced; the volumes players hear come from <c>Config/AudioConfig.asset</c> (SoundLevel on each source).
    /// Loops are rendered a little longer than they play and the extra tail is crossfaded into their start, so the seam
    /// never clicks; their slow modulations repeat a whole number of times per loop. A clip is regenerated when its
    /// Version stamp (the importer's userData) is older than <see cref="Version"/>.
    /// </summary>
    public static class GeneratedAudio
    {
        public const string Folder = "Assets/GameJam/Audio/Generated";
        private const int SampleRate = 44100;
        private const string Version = "gen-v5";

        // Peak level per kind of sound (the AudioSource volumes then sit around 0.6-1).
        private const float PeakEffect = 0.8f;
        private const float PeakLoop = 0.5f;
        private const float PeakMusic = 0.6f;

        /// <summary>A short palm-on-surface thump; pitch varies per variant so repeated taps don't sound robotic.</summary>
        public static AudioClip Tap(string name, float pitchHz, int seed) =>
            Create(name, 0.09f, false, seed, PeakEffect * 0.8f, (t, soft, deep) =>
            {
                float envelope = Mathf.Exp(-t * 55f);
                float body = Mathf.Sin(2f * Mathf.PI * pitchHz * t * (1f - t * 2.5f));
                float click = soft * Mathf.Exp(-t * 400f);
                return (body * 0.75f + click * 0.35f) * envelope;
            });

        /// <summary>Pitch per tap variant, so three clips of each surface never sound robotic in a row.</summary>
        private static float VariantPitch(int variant) => variant switch { 0 => 0.92f, 1 => 1f, _ => 1.09f };

        /// <summary>Palm on grass: a soft low thud into the turf and a short dry rustle of blades.</summary>
        public static AudioClip TapGrass(string name, int variant) =>
            Create(name, 0.16f, false, 40 + variant, PeakEffect * 0.7f, (t, soft, deep) =>
            {
                float p = VariantPitch(variant);
                float thud = Sine(120f * p * (1f - t * 1.5f), t) * Mathf.Exp(-t * 38f);
                float rustle = (soft - deep * 0.3f) * Mathf.Exp(-t * 26f) * (0.55f + 0.45f * Mathf.Sin(t * 210f * p));
                return (thud * 0.7f + rustle * 0.75f) * Mathf.Clamp01(t * 900f);
            });

        /// <summary>Palm on wood: a hollow knock, two woody body modes dying fast, with a little click on contact.</summary>
        public static AudioClip TapWood(string name, int variant) =>
            Create(name, 0.2f, false, 50 + variant, PeakEffect * 0.75f, (t, soft, deep) =>
            {
                float p = VariantPitch(variant);
                float body = Sine(390f * p, t) * Mathf.Exp(-t * 32f) + 0.55f * Sine(1050f * p, t) * Mathf.Exp(-t * 55f);
                float click = soft * Mathf.Exp(-t * 700f);
                return (body + click * 0.5f) * Mathf.Clamp01(t * 3000f);
            });

        /// <summary>Palm on metal: a bright clank, inharmonic partials of a struck plate ringing a little longer.</summary>
        public static AudioClip TapMetal(string name, int variant) =>
            Create(name, 0.5f, false, 60 + variant, PeakEffect * 0.6f, (t, soft, deep) =>
            {
                float p = VariantPitch(variant);
                float ring = Sine(620f * p, t) * Mathf.Exp(-t * 9f)
                             + 0.6f * Sine(1711f * p, t) * Mathf.Exp(-t * 13f)
                             + 0.4f * Sine(3348f * p, t) * Mathf.Exp(-t * 19f)
                             + 0.25f * Sine(5537f * p, t) * Mathf.Exp(-t * 27f);
                float click = soft * Mathf.Exp(-t * 900f);
                return (ring * 0.6f + click * 0.6f) * Mathf.Clamp01(t * 4000f);
            });

        /// <summary>Palm on plastic, inflatables and cloth: a rubbery thunk whose pitch drops, muffled, no ring.</summary>
        public static AudioClip TapPlastic(string name, int variant) =>
            Create(name, 0.16f, false, 70 + variant, PeakEffect * 0.7f, (t, soft, deep) =>
            {
                float p = VariantPitch(variant);
                float thunk = Sine((210f + 180f * Mathf.Exp(-t * 45f)) * p, t) * Mathf.Exp(-t * 30f);
                float muffle = deep * Mathf.Exp(-t * 40f);
                return (thunk + muffle * 0.4f) * Mathf.Clamp01(t * 1500f);
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
            if (IsCurrent(path, out AudioClip existing))
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

            // Filter coefficients were tuned at 22.05 kHz; scale them so the timbre stays the same at 44.1 kHz.
            float rate = 22050f / SampleRate;
            float midNoise = 0f;
            float lowNoise = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                midNoise += (white - midNoise) * 0.35f * rate;
                lowNoise += (white - lowNoise) * 0.08f * rate;
                float crack = (white - midNoise) * Mathf.Exp(-t * 140f) * 0.7f;  // bright, very short: the skin smack
                float puff = lowNoise * Mathf.Exp(-t * 35f) * 3f;              // dull air thup

                float membrane = 0f;
                float bend = 1f - 0.06f * t;                                    // the rubber relaxes a touch as it rings
                for (int k = 0; k < MembraneRatios.Length; k++)
                {
                    float hz = baseHz * MembraneRatios[k] * bend;
                    membrane += MembraneLevels[k] * Mathf.Sin(2f * Mathf.PI * hz * t + phases[k]) * Mathf.Exp(-MembraneDecay[k] * t);
                }

                samples[i] = (membrane * 0.55f + crack + puff) * Mathf.Clamp01(t * 1500f);
            }

            Normalize(samples, PeakEffect);
            return Save(path, samples, 1, false);
        }

        private static float Sine(float hz, float t) => Mathf.Sin(2f * Mathf.PI * hz * t);

        /// <summary>A bell-like note: fundamental plus a soft octave, ringing down.</summary>
        private static float Bell(float hz, float t, float decay) =>
            t < 0f ? 0f : (Sine(hz, t) + 0.35f * Sine(hz * 2f, t) + 0.12f * Sine(hz * 3f, t)) * Mathf.Exp(-t * decay) * Mathf.Clamp01(t * 300f);

        /// <summary>Hoop cleared: three bright bell notes rising (C6 E6 G6).</summary>
        public static AudioClip HoopChime(string name) =>
            Create(name, 1.3f, false, 21, PeakEffect, (t, soft, deep) =>
                Bell(1047f, t, 4.5f) + Bell(1319f, t - 0.09f, 4.5f) + Bell(1568f, t - 0.18f, 3.6f));

        /// <summary>Run won: a short rising fanfare (C5 E5 G5 then a held C6 with a little vibrato).</summary>
        public static AudioClip WinFanfare(string name) =>
            Create(name, 2.4f, false, 22, PeakEffect, (t, soft, deep) =>
            {
                float s = Bell(523f, t, 4f) + Bell(659f, t - 0.16f, 4f) + Bell(784f, t - 0.32f, 4f);
                float held = t - 0.5f;
                if (held > 0f)
                {
                    float vibrato = 1f + 0.006f * Sine(5.5f, held);
                    s += (Sine(1047f * vibrato, held) + 0.4f * Sine(2094f * vibrato, held)) * Mathf.Exp(-held * 1.4f) * Mathf.Clamp01(held * 40f);
                }

                return s;
            });

        /// <summary>
        /// Run won: the backyard critters cheering, about 3.6 s. Birds chirp and trill (quick upward sweeps, 2-4 kHz), frogs
        /// ribbit (a low buzz chopped into pulses), mice and squirrels squeak (short high downward sweeps), over a patter of
        /// tiny paws clapping. Swells in, holds, fades.
        /// </summary>
        public static AudioClip CritterCheer(string name)
        {
            string path = $"{Folder}/{name}.wav";
            if (IsCurrent(path, out AudioClip existing))
            {
                return existing;
            }

            const float seconds = 3.6f;
            var samples = new float[Mathf.RoundToInt(seconds * SampleRate)];
            var random = new System.Random(48);
            float Rand(float a, float b) => a + (float)random.NextDouble() * (b - a);

            for (int i = 0; i < 70; i++) // bird chirps, some in little trills
            {
                float at = Rand(0f, seconds - 0.8f);
                float hz = Rand(2000f, 3800f);
                int notes = random.NextDouble() < 0.35 ? random.Next(3, 6) : 1;
                for (int n = 0; n < notes; n++)
                {
                    Chirp(samples, at + n * 0.055f, Rand(0.05f, 0.1f), hz, hz * Rand(1.25f, 1.7f), Rand(0.25f, 0.45f));
                }
            }

            for (int i = 0; i < 9; i++) // frog ribbits
            {
                Croak(samples, Rand(0f, seconds - 1f), Rand(0.22f, 0.32f), Rand(105f, 160f), Rand(24f, 34f), Rand(0.5f, 0.8f));
            }

            for (int i = 0; i < 22; i++) // squeaks
            {
                float hz = Rand(4200f, 6000f);
                Chirp(samples, Rand(0f, seconds - 0.8f), Rand(0.03f, 0.05f), hz, hz * 0.7f, Rand(0.15f, 0.3f));
            }

            Patter(samples, random, 0.05f, seconds - 0.5f, 55f, 0.08f); // tiny paws clapping

            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SampleRate;
                samples[i] *= Mathf.Clamp01(t / 0.3f) * Mathf.Clamp01((seconds - t) / 1.2f); // swell in, hold, fade
            }

            Normalize(samples, PeakEffect * 0.9f);
            return Save(path, samples, 1, false);
        }

        /// <summary>One chirp or squeak: a sine gliding from one pitch to another with a quick attack and a soft tail.</summary>
        private static void Chirp(float[] samples, float at, float length, float fromHz, float toHz, float gain)
        {
            int start = Mathf.RoundToInt(at * SampleRate);
            int count = Mathf.RoundToInt(length * SampleRate);
            double phase = 0.0;
            for (int k = 0; k < count && start + k < samples.Length; k++)
            {
                float p = (float)k / count;
                phase += 2.0 * Math.PI * Mathf.Lerp(fromHz, toHz, Mathf.Sqrt(p)) / SampleRate;
                float env = Mathf.Clamp01(p * 12f) * (1f - p) * (1f - p);
                samples[start + k] += (float)Math.Sin(phase) * env * gain;
            }
        }

        /// <summary>A frog's ribbit: a buzzy low tone (a few harmonics) chopped into quick pulses, sagging in pitch.</summary>
        private static void Croak(float[] samples, float at, float length, float hz, float pulsesPerSecond, float gain)
        {
            int start = Mathf.RoundToInt(at * SampleRate);
            int count = Mathf.RoundToInt(length * SampleRate);
            double phase = 0.0;
            for (int k = 0; k < count && start + k < samples.Length; k++)
            {
                float t = (float)k / SampleRate;
                float p = (float)k / count;
                phase += 2.0 * Math.PI * hz * (1f - 0.15f * p) / SampleRate;
                float tone = (float)(Math.Sin(phase) + 0.6 * Math.Sin(phase * 2) + 0.35 * Math.Sin(phase * 3) + 0.2 * Math.Sin(phase * 5));
                float pulse = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * pulsesPerSecond * t)), 3f);
                float env = Mathf.Clamp01(p * 20f) * Mathf.Clamp01((1f - p) * 6f);
                samples[start + k] += tone * pulse * env * gain * 0.35f;
            }
        }

        /// <summary>A patter of tiny claps: short noisy ticks at random, perSecond of them between two times.</summary>
        private static void Patter(float[] samples, System.Random random, float from, float to, float perSecond, float gain)
        {
            int ticks = Mathf.RoundToInt((to - from) * perSecond);
            for (int c = 0; c < ticks; c++)
            {
                int start = Mathf.RoundToInt((from + (float)random.NextDouble() * (to - from)) * SampleRate);
                float level = gain * (0.5f + 0.5f * (float)random.NextDouble());
                float low = 0f;
                for (int k = 0; k < SampleRate / 150 && start + k < samples.Length; k++)
                {
                    float white = (float)random.NextDouble() * 2f - 1f;
                    low += (white - low) * 0.5f;
                    samples[start + k] += (white - low) * Mathf.Exp(-k / (SampleRate * 0.002f)) * level;
                }
            }
        }

        /// <summary>
        /// A save (the balloon rescued just above the ground): a quick sparkly run up (G5 B5 D6 G6) with a swish of air,
        /// topped by a high C7 that rings on. Brighter and faster than the hoop chime, so the two never get confused.
        /// </summary>
        public static AudioClip SaveChime(string name) =>
            Create(name, 1.2f, false, 31, PeakEffect, (t, soft, deep) =>
            {
                float run = Bell(784f, t, 7f) + Bell(988f, t - 0.05f, 7f) + Bell(1175f, t - 0.1f, 7f) + Bell(1568f, t - 0.15f, 3.5f);
                float sparkle = 0.55f * Bell(2093f, t - 0.22f, 3f) * (1f + 0.3f * Sine(9f, t));
                float swish = soft * Mathf.Exp(-Mathf.Pow((t - 0.09f) / 0.06f, 2f)) * 0.35f;
                return run * 0.8f + sparkle + swish;
            });

        /// <summary>
        /// A rally milestone: the critters going "ooooh!", about 1.7 s. A flock of birds whistles a long glide up and back
        /// (each at its own pitch, with vibrato), a couple of frogs croak a rising "ooo", and squeaks and paw-pats follow.
        /// The balloon raises its pitch a little for each higher milestone.
        /// </summary>
        public static AudioClip CritterOoh(string name)
        {
            string path = $"{Folder}/{name}.wav";
            if (IsCurrent(path, out AudioClip existing))
            {
                return existing;
            }

            const float seconds = 1.7f;
            var samples = new float[Mathf.RoundToInt(seconds * SampleRate)];
            var random = new System.Random(49);
            float Rand(float a, float b) => a + (float)random.NextDouble() * (b - a);

            for (int v = 0; v < 9; v++) // whistles: up about a fifth over 0.6 s, then sagging
            {
                float f0 = Rand(1300f, 2400f);
                float onset = Rand(0f, 0.12f);
                float level = Rand(0.5f, 0.9f) / 9f;
                float vibrato = Rand(5f, 8f);
                double phase = 0.0;
                int start = Mathf.RoundToInt(onset * SampleRate);
                for (int i = start; i < samples.Length; i++)
                {
                    float t = (float)(i - start) / SampleRate;
                    float slide = 1f + 0.5f * Mathf.Clamp01(t / 0.6f) - 0.18f * Mathf.Clamp01((t - 0.75f) / 0.6f);
                    phase += 2.0 * Math.PI * f0 * slide * (1f + 0.012f * Mathf.Sin(2f * Mathf.PI * vibrato * t)) / SampleRate;
                    float env = Mathf.Clamp01(t / 0.12f) * Mathf.Clamp01((seconds - onset - t) / 0.5f);
                    samples[i] += (float)Math.Sin(phase) * env * level;
                }
            }

            for (int f = 0; f < 2; f++) // frogs: a slow croak under the whistles
            {
                Croak(samples, Rand(0.05f, 0.2f), 0.9f, Rand(95f, 130f), Rand(14f, 20f), 0.35f);
            }

            for (int i = 0; i < 8; i++)
            {
                float hz = Rand(4200f, 5800f);
                Chirp(samples, Rand(0.9f, seconds - 0.15f), 0.04f, hz, hz * 0.7f, Rand(0.08f, 0.15f));
            }

            Patter(samples, random, 0.95f, seconds - 0.1f, 30f, 0.06f);
            Normalize(samples, PeakEffect * 0.85f);
            return Save(path, samples, 1, false);
        }

        /// <summary>
        /// A long rally broken: the sad trombone, "wah wah wah waaah" (Bb3 A3 Ab3, then a long G3 with a wobbling plunger
        /// mute). Brass is a stack of harmonics whose brightness opens and closes on every note (the "wah").
        /// </summary>
        public static AudioClip SadTrombone(string name) =>
            Create(name, 2.6f, false, 33, PeakEffect, (t, soft, deep) =>
            {
                const float shortNote = 0.4f;
                int index = Mathf.Min((int)(t / shortNote), 3);
                float local = t - index * shortNote;
                bool last = index == 3;
                float length = last ? 2.6f - 3f * shortNote : shortNote;
                if (local >= length)
                {
                    return 0f;
                }

                float note = index switch { 0 => 233.1f, 1 => 220f, 2 => 207.7f, _ => 196f };
                float hz = note * (1f - 0.012f * local / length); // each note droops a touch
                float phase;
                float brightness;
                if (last)
                {
                    const float rate = 5.5f, depth = 0.018f;
                    phase = 2f * Mathf.PI * hz * local + hz * depth / rate * (1f - Mathf.Cos(2f * Mathf.PI * rate * local));
                    brightness = 0.35f + 0.35f * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 2.6f * local)) * Mathf.Exp(-local * 0.6f);
                }
                else
                {
                    phase = 2f * Mathf.PI * hz * local;
                    brightness = 0.2f + 0.6f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(local / length));
                }

                float s = 0f;
                for (int n = 1; n <= 9; n++)
                {
                    s += Mathf.Exp(-n / (0.8f + 4f * brightness)) * Mathf.Sin(phase * n);
                }

                float env = Mathf.Clamp01(local / 0.035f) * Mathf.Clamp01((length - local) / (last ? 0.6f : 0.06f));
                return s * env + soft * 0.03f * env; // a breath of air in the bell
            });

        /// <summary>
        /// Time up: a referee's pea whistle, tweet, tweet, tweeeet. A shrill ~2.9 kHz tone frequency-modulated by the
        /// rattling pea (~34 Hz, written in closed form so the pitch never drifts), a little amplitude flutter, a touch of
        /// breath noise, soft edges on every blast.
        /// </summary>
        public static AudioClip RefereeWhistle(string name) =>
            Create(name, 1.7f, false, 27, PeakEffect, (t, soft, deep) =>
            {
                float envelope = WhistleBlast(t, 0f, 0.16f) + WhistleBlast(t, 0.26f, 0.16f) + WhistleBlast(t, 0.52f, 1f);
                if (envelope <= 0f)
                {
                    return 0f;
                }

                float pea = 2f * Mathf.PI * 34f * t;
                float phase = 2f * Mathf.PI * 2900f * t + 3.5f * Mathf.Sin(pea);
                float tone = Mathf.Sin(phase) + 0.25f * Mathf.Sin(2f * phase);
                float flutter = 0.78f + 0.22f * Mathf.Sin(pea);
                return (tone * flutter + soft * 0.08f) * envelope;
            });

        private static float WhistleBlast(float t, float start, float length)
        {
            float local = t - start;
            if (local < 0f || local > length + 0.05f)
            {
                return 0f;
            }

            float attack = Mathf.Clamp01(local / 0.012f);
            float release = local > length ? 1f - (local - length) / 0.05f : 1f;
            return attack * release;
        }

        /// <summary>
        /// Diving into a mole hole: a gritty scrape of soil, then a rounded "bwoop" dropping away as the player goes down.
        /// </summary>
        public static AudioClip MoleDive(string name) =>
            Create(name, 0.9f, false, 41, PeakEffect, (t, soft, deep) =>
            {
                float scrape = soft * (0.55f + 0.45f * Sine(31f, t)) * Mathf.Exp(-t * 7f) * Mathf.Clamp01(t * 80f);
                float drop = t - 0.12f;
                float bwoop = 0f;
                if (drop > 0f)
                {
                    // Pitch falls from 330 Hz to 70 Hz; the phase is the integral of that sweep, so it glides cleanly.
                    float phase = 2f * Mathf.PI * (70f * drop + (260f / 5.5f) * (1f - Mathf.Exp(-drop * 5.5f)));
                    bwoop = Mathf.Sin(phase) * Mathf.Exp(-drop * 3.2f) * Mathf.Clamp01(drop * 60f);
                }

                return scrape * 0.7f + bwoop * 0.9f + deep * 0.25f * Mathf.Exp(-t * 4f);
            });

        /// <summary>
        /// Falling down the mole tunnel: a seamless loop of deep rushing air with soil rattling past. The swells repeat a
        /// whole number of times per loop, so the seam is invisible.
        /// </summary>
        public static AudioClip TunnelRush(string name)
        {
            const float length = 3f;
            return Create(name, length, true, 42, PeakLoop, (t, soft, deep) =>
            {
                float phase = 2f * Mathf.PI * t / length;
                float swell = 0.8f + 0.15f * Mathf.Sin(phase * 2f) + 0.05f * Mathf.Sin(phase * 5f + 0.7f);
                float rattle = soft * (0.5f + 0.5f * Mathf.Sin(phase * 36f)) * 0.22f;
                return deep * swell + rattle;
            });
        }

        /// <summary>Landing at the end of the tunnel: a soft, heavy thud into packed soil with a puff of loose dirt.</summary>
        public static AudioClip LandThud(string name) =>
            Create(name, 0.55f, false, 43, PeakEffect, (t, soft, deep) =>
            {
                float thump = Sine(78f * (1f - t * 0.7f), t) * Mathf.Exp(-t * 13f);
                float dirt = soft * Mathf.Exp(-t * 22f) * 0.55f;
                float scatter = soft * Mathf.Exp(-(t - 0.12f) * (t - 0.12f) * 300f) * 0.2f;
                return (thump + dirt + scatter) * Mathf.Clamp01(t * 1500f);
            });

        /// <summary>
        /// A tutorial gate sinking into the floor: soil and roots grinding for a second and a half (a low rumble that
        /// judders), ending in a dull thunk as it settles.
        /// </summary>
        public static AudioClip GateRumble(string name) =>
            Create(name, 1.9f, false, 45, PeakEffect, (t, soft, deep) =>
            {
                float grind = Mathf.Clamp01(t * 8f) * Mathf.Clamp01((1.6f - t) * 6f);
                float judder = 0.65f + 0.35f * Sine(23f, t) * Sine(3.1f, t);
                float rumble = (deep * 0.9f + soft * 0.3f) * judder * grind + Sine(52f, t) * 0.25f * grind;
                float settle = t - 1.55f;
                float thunk = settle > 0f ? Sine(64f * (1f - settle), settle) * Mathf.Exp(-settle * 14f) * 1.3f : 0f;
                return rumble + thunk;
            });

        /// <summary>A tutorial lesson ticked off: two quick bright notes going up (G6 C7), shorter than the hoop chime.</summary>
        public static AudioClip LessonDone(string name) =>
            Create(name, 0.9f, false, 44, PeakEffect * 0.9f, (t, soft, deep) =>
                Bell(1568f, t, 7f) + Bell(2093f, t - 0.11f, 5f));

        /// <summary>
        /// A giant's kick: a swish of air as the leg swings through, then a heavy, padded thump 0.2 s in (the swing ends and
        /// the foot lands there, MischiefMaker plays it as the Kick swing starts).
        /// </summary>
        public static AudioClip GiantKick(string name) =>
            Create(name, 0.7f, false, 46, PeakEffect, (t, soft, deep) =>
            {
                float swish = soft * Mathf.Exp(-(t - 0.12f) * (t - 0.12f) * 90f) * 0.7f;
                float hit = t - 0.2f;
                float thump = hit > 0f
                    ? (Sine(70f * (1f - hit * 0.6f), hit) * Mathf.Exp(-hit * 11f) + deep * 0.5f * Mathf.Exp(-hit * 18f)) * Mathf.Clamp01(hit * 1500f)
                    : 0f;
                return swish + thump * 1.2f;
            });

        /// <summary>Trampoline: a springy boing (a pitch that drops and wobbles).</summary>
        public static AudioClip TrampolineBoing(string name) =>
            Create(name, 0.6f, false, 23, PeakEffect * 0.85f, (t, soft, deep) =>
            {
                float pitch = 140f + 260f * Mathf.Exp(-t * 6f) + 25f * Sine(14f, t) * Mathf.Exp(-t * 3f);
                return Sine(pitch, t) * Mathf.Exp(-t * 4.5f) * Mathf.Clamp01(t * 200f);
            });

        /// <summary>Balloon pop: a short, rounded crack of air with a low thump under it (not a harsh noise burst).</summary>
        public static AudioClip BalloonPop(string name) =>
            Create(name, 0.4f, false, 24, PeakEffect, (t, soft, deep) =>
            {
                float crack = soft * Mathf.Exp(-t * 60f) * 2.2f;
                float thump = Sine(85f * (1f - t), t) * Mathf.Exp(-t * 16f);
                return (crack + thump) * Mathf.Clamp01(t * 2000f);
            });

        /// <summary>Bubble wand: a seamless loop of little bubbly blips at scattered times and pitches.</summary>
        public static AudioClip BubbleLoop(string name)
        {
            const float length = 2f;
            return Create(name, length, true, 25, PeakLoop, (t, soft, deep) =>
            {
                float s = 0f;
                for (int k = 0; k < 14; k++)
                {
                    // Onsets spread over the loop; the crossfade tail (t > length) repeats the first ones, so bubbles
                    // near the seam carry across it.
                    float onset = Mathf.Repeat(k * 0.618034f, 1f) * length;
                    for (int wrap = 0; wrap < 2; wrap++)
                    {
                        float local = t - onset - wrap * length;
                        if (local < 0f || local > 0.12f)
                        {
                            continue;
                        }

                        float baseHz = 420f + Mathf.Repeat(k * 0.381966f, 1f) * 480f;
                        s += Sine(baseHz * (1f + local * 9f), local) * Mathf.Exp(-local * 35f) * Mathf.Clamp01(local * 400f);
                    }
                }

                return s;
            });
        }

        /// <summary>
        /// Wind (the box fan and the leaf blower): a seamless loop of soft, deep rushing air that swells gently. The
        /// swells repeat exactly once and twice per loop, so the seam is invisible.
        /// </summary>
        public static AudioClip WindLoop(string name)
        {
            const float length = 4f;
            return Create(name, length, true, 26, PeakLoop, (t, soft, deep) =>
            {
                float phase = 2f * Mathf.PI * t / length;
                float swell = 0.75f + 0.18f * Mathf.Sin(phase) + 0.07f * Mathf.Sin(2f * phase + 1.3f);
                return (deep * 0.85f + soft * 0.15f) * swell;
            });
        }

        /// <summary>A gentle, warm hiss for sliding hands; loops without a click.</summary>
        public static AudioClip SlideLoop(string name) =>
            Create(name, 1f, true, 7, PeakLoop * 0.6f, (t, soft, deep) => soft * 0.5f + deep * 0.5f);

        /// <summary>
        /// Background music: an upbeat, groovy pop loop (catchy, not a nursery tune) that loops seamlessly. 116 BPM, 16
        /// bars (~33 s) over Am F C G: four-on-the-floor kick, clap on 2 and 4, swung 16th hi-hats with open hats on the
        /// off-beats, a syncopated filtered bass and warm detuned pad chords that pump with the kick. No lead melody: the
        /// bell-like plucked hook was taken out on request, the groove carries it. Stereo, a little room
        /// reverb; everything ringing past the loop end is folded back onto the start, so the seam can't be heard.
        /// </summary>
        public static AudioClip MusicLoop(string name)
        {
            string path = $"{Folder}/{name}.wav";
            if (IsCurrent(path, out AudioClip existing))
            {
                return existing;
            }

            float sixteenth = 60f / MusicTempo / 4f;
            float bar = sixteenth * 16f;
            int bars = MusicChords.Length * 4;
            int loopLength = Mathf.RoundToInt(bar * bars * SampleRate);
            int tail = SampleRate * 3;
            var left = new float[loopLength + tail];
            var right = new float[loopLength + tail];
            var random = new System.Random(77);
            float Step(int b, int s) => b * bar + s * sixteenth + (s % 2 == 1 ? sixteenth * MusicSwing : 0f);

            // Kick times, for the pad and bass "pump" (sidechain).
            var kicks = new List<float>();
            for (int b = 0; b < bars; b++)
            {
                int[] chord = Chord(MusicChords[b % MusicChords.Length]);
                for (int s = 0; s < 16; s++)
                {
                    float time = Step(b, s);
                    if (s % 4 == 0)
                    {
                        kicks.Add(time);
                        AddVoice(left, right, time, Kick(), 0.55f, 0f);
                    }

                    if (s == 4 || s == 12)
                    {
                        AddVoice(left, right, time, Clap(random), 0.3f, 0.05f);
                    }

                    // Hats: open on the off-beat eighths, closed and quieter on the other 16ths.
                    bool open = s % 4 == 2;
                    AddVoice(left, right, time, Hat(random, open), open ? 0.11f : (s % 2 == 0 ? 0.05f : 0.035f), open ? 0.35f : 0.5f);
                }

                // Syncopated bass: (16th step, interval above the root, length in 16ths).
                foreach ((int step, int interval, int length) in BassPattern)
                {
                    AddVoice(left, right, Step(b, step), SynthBass(Hz(chord[0] - 24 + interval), length * sixteenth), 0.3f, 0f);
                }

                // Pad: the chord plus the root an octave up, held for the bar.
                foreach (int note in new[] { chord[0], chord[1], chord[2], chord[0] + 12 })
                {
                    AddVoice(left, right, Step(b, 0), Pad(Hz(note), bar), 0.055f, note % 2 == 0 ? -0.4f : 0.4f);
                }
            }

            // Pump: duck everything but the drums a little right after each kick (pads and bass breathe with the beat).
            // Applied to the whole mix gently, with the drums above already landing on the kick, it reads as groove.
            for (int i = 0; i < left.Length; i++)
            {
                float t = (float)i / SampleRate;
                float since = t - kicks[Mathf.Clamp(Mathf.FloorToInt(t / (sixteenth * 4f)), 0, kicks.Count - 1)];
                float duck = since >= 0f ? 1f - 0.18f * Mathf.Exp(-since * 9f) : 1f;
                left[i] *= duck;
                right[i] *= duck;
            }

            Reverb(left, right, 0.1f);
            SoftLowPass(left, 12000f);
            SoftLowPass(right, 12000f);

            // Fold the ringing past the loop end back onto the start: seamless.
            for (int i = 0; i < tail; i++)
            {
                left[i] += left[loopLength + i];
                right[i] += right[loopLength + i];
            }

            var interleaved = new float[loopLength * 2];
            for (int i = 0; i < loopLength; i++)
            {
                interleaved[i * 2] = left[i];
                interleaved[i * 2 + 1] = right[i];
            }

            Normalize(interleaved, PeakMusic);
            return Save(path, interleaved, 2, true);
        }

        private const float MusicTempo = 116f;
        private const float MusicSwing = 0.12f; // fraction of a 16th the off 16ths are late: a little bounce
        private static readonly string[] MusicChords = { "Am", "F", "C", "G" };

        // (16th step, semitones above the root, length in 16ths): root, a ghost root, the octave, root, ghost, the fifth.
        private static readonly (int, int, int)[] BassPattern =
        {
            (0, 0, 3), (3, 0, 1), (6, 12, 2), (8, 0, 3), (11, 0, 1), (14, 7, 2),
        };

        private static int[] Chord(string name) => name switch
        {
            "Am" => new[] { 57, 60, 64 },
            "F" => new[] { 53, 57, 60 },
            "C" => new[] { 48, 52, 55 },
            "G" => new[] { 55, 59, 62 },
            _ => new[] { 57, 60, 64 },
        };

        private static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        /// <summary>A band-limited sawtooth sample (PolyBLEP), phase in [0, 1).</summary>
        private static float Saw(float phase, float increment)
        {
            float value = 2f * phase - 1f;
            if (phase < increment)
            {
                float t = phase / increment;
                value -= t + t - t * t - 1f;
            }
            else if (phase > 1f - increment)
            {
                float t = (phase - 1f) / increment;
                value -= t * t + t + t + 1f;
            }

            return value;
        }

        /// <summary>Warm pad: two detuned saws, darkened, a slow swell and release over the bar.</summary>
        private static float[] Pad(float hz, float seconds)
        {
            int count = Mathf.RoundToInt((seconds + 0.25f) * SampleRate);
            var output = new float[count];
            float a = 0f, b = 0f, low1 = 0f, low2 = 0f;
            float cut = 1f - Mathf.Exp(-2f * Mathf.PI * 1600f / SampleRate);
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float incA = hz * 1.004f / SampleRate, incB = hz * 0.996f / SampleRate;
                a = Mathf.Repeat(a + incA, 1f);
                b = Mathf.Repeat(b + incB, 1f);
                float raw = (Saw(a, incA) + Saw(b, incB)) * 0.5f;
                low1 += (raw - low1) * cut;
                low2 += (low1 - low2) * cut;
                float swell = Mathf.Clamp01(t / 0.06f) * (t > seconds ? Mathf.Exp(-(t - seconds) * 18f) : 1f);
                output[i] = low2 * swell;
            }

            return output;
        }

        /// <summary>Synth bass: a saw an octave down plus a sine sub, through a low-pass that closes after the pluck.</summary>
        private static float[] SynthBass(float hz, float seconds)
        {
            int count = Mathf.RoundToInt((seconds + 0.04f) * SampleRate);
            var output = new float[count];
            float phase = 0f, low = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float inc = hz / SampleRate;
                phase = Mathf.Repeat(phase + inc, 1f);
                float cutoff = 220f + 900f * Mathf.Exp(-t * 18f);
                low += (Saw(phase, inc) - low) * (1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SampleRate));
                float sub = Mathf.Sin(2f * Mathf.PI * phase) * 0.8f;
                float envelope = Mathf.Clamp01(t * 400f) * (t > seconds ? Mathf.Exp(-(t - seconds) * 80f) : 1f);
                output[i] = (low * 0.7f + sub) * envelope;
            }

            return output;
        }

        /// <summary>Kick: a punchy sine sweep (150 down to 48 Hz) with a little click on top.</summary>
        private static float[] Kick()
        {
            int count = Mathf.RoundToInt(0.35f * SampleRate);
            var output = new float[count];
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                phase += 2f * Mathf.PI * (48f + 102f * Mathf.Exp(-t * 35f)) / SampleRate;
                float click = Mathf.Exp(-t * 400f) * 0.3f;
                output[i] = (Mathf.Sin(phase) * Mathf.Exp(-t * 9f) + click) * Mathf.Clamp01(t * 3000f);
            }

            return output;
        }

        /// <summary>Clap: three quick bursts of band-passed noise and a short tail.</summary>
        private static float[] Clap(System.Random random)
        {
            int count = Mathf.RoundToInt(0.25f * SampleRate);
            var output = new float[count];
            float low = 0f, band = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                low += (white - low) * 0.45f;
                band += ((white - low) - band) * 0.35f;
                float bursts = 0f;
                foreach (float onset in new[] { 0f, 0.011f, 0.022f })
                {
                    float local = t - onset;
                    bursts += local >= 0f ? Mathf.Exp(-local * 180f) : 0f;
                }

                output[i] = band * (bursts + 0.6f * Mathf.Exp(-t * 18f) * Mathf.Clamp01((t - 0.022f) * 500f));
            }

            return output;
        }

        /// <summary>Hi-hat: high-passed noise, closed (very short) or open (rings a little).</summary>
        private static float[] Hat(System.Random random, bool open)
        {
            int count = Mathf.RoundToInt((open ? 0.16f : 0.05f) * SampleRate);
            var output = new float[count];
            float low = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                low += (white - low) * 0.6f;
                output[i] = (white - low) * Mathf.Exp(-t * (open ? 22f : 90f)) * Mathf.Clamp01(t * 3000f);
            }

            return output;
        }

        /// <summary>Adds a voice into the stereo mix at a time, with an equal-power pan (-1 left .. 1 right).</summary>
        private static void AddVoice(float[] left, float[] right, float time, float[] voice, float level, float pan)
        {
            int start = Mathf.RoundToInt(time * SampleRate);
            float angle = (pan + 1f) * Mathf.PI * 0.25f;
            float l = Mathf.Cos(angle) * level * 1.41421f;
            float r = Mathf.Sin(angle) * level * 1.41421f;
            for (int i = 0; i < voice.Length && start + i < left.Length; i++)
            {
                left[start + i] += voice[i] * l;
                right[start + i] += voice[i] * r;
            }
        }

        /// <summary>A small Schroeder room (4 combs + 2 allpasses per side, different delays left and right), mixed in.</summary>
        private static void Reverb(float[] left, float[] right, float wet)
        {
            float[] Room(float[] dry, int[] combs, int[] allpasses)
            {
                var output = new float[dry.Length];
                foreach (int delay in combs)
                {
                    var buffer = new float[delay];
                    float damp = 0f;
                    for (int i = 0; i < dry.Length; i++)
                    {
                        float delayed = buffer[i % delay];
                        damp += (delayed - damp) * 0.45f; // darker tail
                        buffer[i % delay] = dry[i] + damp * 0.78f;
                        output[i] += delayed * 0.25f;
                    }
                }

                foreach (int delay in allpasses)
                {
                    var buffer = new float[delay];
                    for (int i = 0; i < output.Length; i++)
                    {
                        float delayed = buffer[i % delay];
                        float input = output[i];
                        buffer[i % delay] = input + delayed * 0.5f;
                        output[i] = delayed - input * 0.5f;
                    }
                }

                return output;
            }

            float[] roomLeft = Room(left, new[] { 1557, 1617, 1491, 1422 }, new[] { 225, 556 });
            float[] roomRight = Room(right, new[] { 1580, 1640, 1514, 1445 }, new[] { 248, 579 });
            for (int i = 0; i < left.Length; i++)
            {
                left[i] += roomLeft[i] * wet;
                right[i] += roomRight[i] * wet;
            }
        }

        private static void SoftLowPass(float[] samples, float cutoffHz)
        {
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoffHz / SampleRate);
            float state = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                state += (samples[i] - state) * a;
                samples[i] = state;
            }
        }

        private static void FadeOut(float[] samples, float seconds)
        {
            int fade = Mathf.Min(samples.Length, Mathf.RoundToInt(seconds * SampleRate));
            for (int i = 0; i < fade; i++)
            {
                samples[samples.Length - 1 - i] *= (float)i / fade;
            }
        }

        private static void Normalize(float[] samples, float peakTarget)
        {
            float peak = 0.0001f;
            foreach (float sample in samples)
            {
                peak = Mathf.Max(peak, Mathf.Abs(sample));
            }

            float gain = peakTarget / peak;
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= gain;
            }
        }

        /// <summary>
        /// Renders a mono clip. sample(t, soft, deep): soft = gently low-passed noise, deep = darker, rumbling noise (wind).
        /// Loops render an extra 15% that is crossfaded (equal power) into their start, then cut to the exact length.
        /// </summary>
        private static AudioClip Create(string name, float seconds, bool loop, int seed, float peak, Func<float, float, float, float> sample)
        {
            string path = $"{Folder}/{name}.wav";
            if (IsCurrent(path, out AudioClip existing))
            {
                return existing;
            }

            int count = Mathf.RoundToInt(seconds * SampleRate);
            int fade = loop ? count * 15 / 100 : 0;
            var samples = new float[count + fade];
            var random = new System.Random(seed);
            float soft = 0f, deep1 = 0f, deep2 = 0f, rumble = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                soft += (white - soft) * 0.12f;
                deep1 += (white - deep1) * 0.03f;
                deep2 += (deep1 - deep2) * 0.05f;
                rumble += (deep2 - rumble) * 0.002f; // take out the sub-bass so it doesn't boom
                samples[i] = sample((float)i / SampleRate, soft * 2.5f, (deep2 - rumble) * 14f);
            }

            if (loop)
            {
                for (int i = 0; i < fade; i++)
                {
                    float w = (float)i / fade * Mathf.PI * 0.5f;
                    samples[i] = samples[i] * Mathf.Sin(w) + samples[count + i] * Mathf.Cos(w);
                }

                Array.Resize(ref samples, count);
            }

            Normalize(samples, peak);
            return Save(path, samples, 1, false);
        }

        private static bool IsCurrent(string path, out AudioClip clip)
        {
            clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            return clip != null && AssetImporter.GetAtPath(path) is AudioImporter importer && importer.userData == Version;
        }

        /// <summary>
        /// Effects and loops: uncompressed PCM, decompressed on load (tiny, no decoding, loops sample-exact). Music: Vorbis
        /// compressed in memory (loops gaplessly, unlike MP3), loaded in the background.
        /// </summary>
        private static AudioClip Save(string path, float[] samples, int channels, bool isMusic)
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllBytes(path, ToWav(samples, channels));
            AssetDatabase.ImportAsset(path);

            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono = channels == 1;
            importer.loadInBackground = isMusic;
            importer.userData = Version;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = isMusic ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = isMusic ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            settings.quality = isMusic ? 0.6f : 1f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static byte[] ToWav(float[] samples, int channels)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            int dataBytes = samples.Length * 2;
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataBytes);
            writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write((short)channels);
            writer.Write(SampleRate);
            writer.Write(SampleRate * 2 * channels);
            writer.Write((short)(2 * channels));
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
