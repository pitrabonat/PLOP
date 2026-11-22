using System;
using UnityEngine;

namespace Plop.Presentation
{
    internal enum PlopCue { Button, Move, Bump, Settle, Complete, WorldEntrance, Count }

    /// <summary>Original bells and damped tones rendered to small mono clips when first needed.</summary>
    internal static class PlopAudioClips
    {
        private const int SampleRate = 24_000;
        private const double Tau = 2.0 * Math.PI;

        public static AudioClip CreateCue(PlopCue cue)
        {
            switch (cue)
            {
                case PlopCue.Button: return Chirp("Soft tap", 0.084, 655, 455, 0.025, 0.23, 1.9);
                case PlopCue.Move: return Chirp("Jelly plop", 0.136, 470, 250, 0.043, 0.37, 2.15);
                case PlopCue.Bump: return Chirp("Pebble knock", 0.108, 245, 118, 0.025, 0.32, 2.85);
                case PlopCue.Settle: return Settlement();
                case PlopCue.Complete: return Completion();
                case PlopCue.WorldEntrance: return WorldEntrance();
                default: throw new ArgumentOutOfRangeException(nameof(cue));
            }
        }

        public static AudioClip CreateMusic(WorldAmbience ambience)
        {
            switch (ambience)
            {
                case WorldAmbience.DewdropGarden:
                    return WorldLoop("Dewdrop Garden", 1.75, 0.025, 0.016,
                        new[] { 55, 57, 52, 55, 57, 50, 52, 55 },
                        new[] { 79, 74, 76, 81, 76, 74, 71, 79 });
                case WorldAmbience.RainyWindow:
                    return WorldLoop("Rainy Window", 2.4, 0.004, 0.036,
                        new[] { 48, 53, 45, 50, 46, 48 },
                        new[] { 67, 69, 64, 72, 65, 67 });
                case WorldAmbience.TwilightAttic:
                    return WorldLoop("Twilight Attic", 2.15, 0.007, 0.008,
                        new[] { 43, 48, 50, 45, 41, 43 },
                        new[] { 62, 64, 67, 64, 60, 62 });
                case WorldAmbience.MoonlitClouds:
                    return WorldLoop("Moonlit Clouds", 2.5, 0.012, 0.011,
                        new[] { 45, 48, 53, 52, 50, 45 },
                        new[] { 81, 84, 79, 76, 77, 81 });
                default:
                    return CozyLoop();
            }
        }

        private static AudioClip CozyLoop()
        {
            const double bar = 2.0;
            var roots = new[] { 48, 45, 50, 43, 48, 41, 45, 43 };
            var fifths = new[] { 55, 52, 57, 50, 55, 48, 52, 50 };
            var melody = new[] { 64, 67, 65, 62, 67, 69, 64, 62 };
            var answers = new[] { 67, 72, 69, 67, 74, 72, 69, 64 };
            var pcm = Samples(bar * roots.Length);

            for (int i = 0; i < roots.Length; i++)
            {
                double start = i * bar;
                Bell(pcm, start + 0.02, 1.89, roots[i], 0.14, 0.045);
                Bell(pcm, start + 0.41, 1.37, fifths[i], 0.11, 0.027);
                Bell(pcm, start + 0.22, 1.06, melody[i], 0.10, 0.062);
                Bell(pcm, start + 1.21, 0.69, answers[i], 0.10, 0.040);
            }

            return Clip("Cozy Nest", pcm);
        }

        private static AudioClip WorldLoop(string name, double bar, double noiseResponse,
            double noiseGain, int[] roots, int[] melody)
        {
            var pcm = Samples(bar * roots.Length);
            AddAir(pcm, noiseResponse, noiseGain);
            for (int i = 0; i < roots.Length; i++)
            {
                double start = i * bar;
                Bell(pcm, start + 0.07, bar * 0.92, roots[i], 0.27, 0.09);
                Bell(pcm, start + 0.32, bar * 0.57, melody[i], 0.14, 0.048);
                Bell(pcm, start + bar * 0.58, bar * 0.40, melody[i] - 5, 0.12, 0.029);
            }
            return Clip(name, pcm);
        }

        private static void AddAir(float[] pcm, double response, double gain)
        {
            // Deterministic filtered air, tapered at both ends of the loop.
            uint noise = 0x8AE7BA31u;
            double filtered = 0;
            const int edge = SampleRate / 6;
            for (int i = 0; i < pcm.Length; i++)
            {
                noise = unchecked(1664525u * noise + 1013904223u);
                double white = ((noise >> 16) / 32767.5) - 1.0;
                filtered += response * (white - filtered);
                double fade = Math.Min(1.0, Math.Min(i, pcm.Length - i - 1) / (double)edge);
                pcm[i] = (float)(filtered * gain * fade);
            }
        }

        private static AudioClip WorldEntrance()
        {
            var pcm = Samples(0.55);
            Bell(pcm, 0.00, 0.34, 67, 0.045, 0.18);
            Bell(pcm, 0.12, 0.40, 74, 0.060, 0.16);
            return Clip("World entrance", pcm);
        }

        private static AudioClip Settlement()
        {
            var pcm = Samples(0.43);
            Bell(pcm, 0.00, 0.31, 72, 0.018, 0.29);
            Bell(pcm, 0.09, 0.33, 76, 0.031, 0.19);
            return Clip("Warm nest", pcm);
        }

        private static AudioClip Completion()
        {
            var pcm = Samples(0.91);
            Bell(pcm, 0.00, 0.44, 70, 0.017, 0.23);
            Bell(pcm, 0.11, 0.46, 74, 0.025, 0.18);
            Bell(pcm, 0.25, 0.50, 77, 0.025, 0.21);
            Bell(pcm, 0.38, 0.51, 82, 0.045, 0.15);
            return Clip("Sweet dreams", pcm);
        }

        private static AudioClip Chirp(string name, double duration, double fromHz, double toHz,
            double attack, double gain, double overtone)
        {
            var pcm = Samples(duration);
            double sweep = (toHz - fromHz) / duration;
            for (int i = 0; i < pcm.Length; i++)
            {
                double t = (double)i / SampleRate;
                double phase = Tau * t * (fromHz + sweep * t * 0.5);
                double envelope = Envelope(t, duration, attack, 3.6);
                pcm[i] = (float)(gain * envelope * (Math.Sin(phase) + 0.15 * Math.Sin(phase * overtone)));
            }
            return Clip(name, pcm);
        }

        private static void Bell(float[] pcm, double start, double duration, int midi, double attack, double gain)
        {
            int first = (int)Math.Round(start * SampleRate);
            int count = Math.Min((int)Math.Round(duration * SampleRate), pcm.Length - first);
            double frequency = 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);
            for (int i = 0; i < count; i++)
            {
                double t = (double)i / SampleRate;
                double phase = Tau * t * frequency;
                double envelope = Envelope(t, duration, attack, 2.7);
                double sample = Math.Sin(phase) + 0.13 * Math.Sin(phase * 2.01) + 0.045 * Math.Sin(phase * 3.02);
                pcm[first + i] += (float)(gain * envelope * sample);
            }
        }

        private static double Envelope(double time, double duration, double attack, double decay)
        {
            double fadeIn = Math.Min(1.0, time / attack);
            double fadeOut = Math.Clamp((duration - time) / (duration * 0.31), 0, 1);
            return fadeIn * fadeIn * Math.Exp(-decay * time / duration) * fadeOut * fadeOut;
        }

        private static float[] Samples(double duration) => new float[(int)Math.Round(duration * SampleRate)];

        private static AudioClip Clip(string name, float[] pcm)
        {
            var clip = AudioClip.Create(name, pcm.Length, 1, SampleRate, stream: false);
            clip.SetData(pcm, 0);
            return clip;
        }
    }
}
