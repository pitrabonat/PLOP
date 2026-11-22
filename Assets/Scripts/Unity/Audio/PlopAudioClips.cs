using System;
using UnityEngine;

namespace Plop.Presentation
{
    internal enum PlopCue { Button, Move, Wall, Plop, Settle, Undo, Restart, Complete, WorldUnlock, Deadlock, Count }

    /// <summary>Original short rubber, felt and hardwood textures with soft bell accents.</summary>
    internal static class PlopAudioClips
    {
        private const int SampleRate = 24_000;
        private const double Tau = 2.0 * Math.PI;

        public static AudioClip CreateCue(PlopCue cue)
        {
            switch (cue)
            {
                case PlopCue.Button: return Tap("Soft tap", 0.075, 585, 405, 0.015, 0.19, 0.07, 0.14, 91u);
                case PlopCue.Move: return Tap("Jelly plop", 0.125, 440, 211, 0.022, 0.32, 0.09, 0.10, 37u);
                case PlopCue.Wall: return Tap("Pebble knock", 0.095, 321, 164, 0.011, 0.24, 0.18, 0.21, 17u);
                case PlopCue.Plop: return Tap("Rubber bounce", 0.132, 279, 364, 0.020, 0.25, 0.07, 0.047, 21u);
                case PlopCue.Settle: return Settlement();
                case PlopCue.Undo: return ChangingCourse("Soft rewind", 76, 69, 0.082);
                case PlopCue.Restart: return ChangingCourse("Fresh start", 65, 72, 0.116);
                case PlopCue.Complete: return Completion();
                case PlopCue.WorldUnlock: return WorldUnlock();
                case PlopCue.Deadlock: return Deadlock();
                default: throw new ArgumentOutOfRangeException(nameof(cue));
            }
        }

        private static AudioClip Settlement()
        {
            var pcm = Samples(0.38);
            AddTap(pcm, 0.095, 202, 126, 0.016, 0.12, 0.07, 0.06, 53u);
            Bell(pcm, 0.015, 0.27, 72, 0.023, 0.19);
            Bell(pcm, 0.086, 0.29, 76, 0.041, 0.13);
            return Clip("Nest tuck", pcm);
        }

        private static AudioClip ChangingCourse(string name, int first, int second, double gap)
        {
            var pcm = Samples(0.29);
            Bell(pcm, 0.00, 0.17, first, 0.022, 0.10);
            Bell(pcm, gap, 0.17, second, 0.027, 0.15);
            return Clip(name, pcm);
        }

        private static AudioClip Completion()
        {
            var pcm = Samples(0.64);
            Bell(pcm, 0.00, 0.33, 67, 0.034, 0.16);
            Bell(pcm, 0.09, 0.37, 71, 0.035, 0.16);
            Bell(pcm, 0.21, 0.42, 74, 0.054, 0.17);
            return Clip("Sweet dreams", pcm);
        }

        private static AudioClip WorldUnlock()
        {
            var pcm = Samples(0.55);
            Bell(pcm, 0.00, 0.31, 62, 0.052, 0.15);
            Bell(pcm, 0.11, 0.42, 69, 0.062, 0.13);
            return Clip("Welcome to a new world", pcm);
        }

        private static AudioClip Deadlock()
        {
            var pcm = Samples(0.40);
            Bell(pcm, 0.00, 0.25, 69, 0.041, 0.14);
            Bell(pcm, 0.12, 0.27, 65, 0.045, 0.12);
            return Clip("Try a new path", pcm);
        }

        private static AudioClip Tap(string name, double duration, double fromHz, double toHz,
            double attack, double gain, double noiseAmount, double response, uint seed)
        {
            var pcm = Samples(duration);
            AddTap(pcm, duration, fromHz, toHz, attack, gain, noiseAmount, response, seed);
            return Clip(name, pcm);
        }

        private static void AddTap(float[] pcm, double duration, double fromHz, double toHz,
            double attack, double gain, double noiseAmount, double response, uint noise)
        {
            double sweep = (toHz - fromHz) / duration;
            double filtered = 0.0;
            int count = Math.Min((int)Math.Round(duration * SampleRate), pcm.Length);
            for (int i = 0; i < count; i++)
            {
                noise = unchecked(1664525u * noise + 1013904223u);
                double white = ((noise >> 16) / 32767.5) - 1.0;
                filtered += response * (white - filtered);

                double t = (double)i / SampleRate;
                double phase = Tau * t * (fromHz + sweep * t * 0.5);
                double envelope = Envelope(t, duration, attack, 3.2);
                double tone = Math.Sin(phase) + 0.15 * Math.Sin(phase * 2.13);
                pcm[i] += (float)(gain * envelope * (tone + noiseAmount * filtered));
            }
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
                double sample = Math.Sin(phase) + 0.10 * Math.Sin(phase * 2.01) + 0.035 * Math.Sin(phase * 3.02);
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
