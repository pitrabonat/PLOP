// PLOP! offline audio synthesis — original modal/additive instrument models (no samples).
namespace PlopAudioSynth;

public enum Instr { FeltPiano, MusicBox, Kalimba, Marimba, Bells, Celesta, Flute, Pad, Bass, Pluck }

public static class Instruments
{
    /// <summary>Renders one note into <paramref name="b"/>. Times in seconds, velocity 0..1.</summary>
    public static void Play(Buffer b, Instr instr, double start, double midi, double duration, double vel, double pan, uint seed)
    {
        var rng = new Rng(seed * 2654435761u + (uint)(midi * 977) + 17);
        switch (instr)
        {
            case Instr.FeltPiano: FeltPiano(b, start, midi, duration, vel, pan, rng); break;
            case Instr.MusicBox: Modal(b, start, midi, vel, pan, rng, new[] { 1.0, 6.27, 17.55, 2.0 }, new[] { 1.0, 0.22, 0.05, 0.12 }, new[] { 1.9, 0.18, 0.05, 0.6 }, click: 0.05, attack: 0.0015); break;
            case Instr.Kalimba: Modal(b, start, midi, vel, pan, rng, new[] { 1.0, 5.93, 12.1, 2.0 }, new[] { 1.0, 0.28, 0.07, 0.10 }, new[] { 1.5, 0.22, 0.07, 0.4 }, click: 0.07, attack: 0.002); break;
            case Instr.Marimba: Modal(b, start, midi, vel, pan, rng, new[] { 1.0, 3.93, 9.2, 2.0 }, new[] { 1.0, 0.30, 0.08, 0.05 }, new[] { 0.75, 0.16, 0.05, 0.3 }, click: 0.09, attack: 0.003); break;
            case Instr.Bells: Modal(b, start, midi, vel, pan, rng, new[] { 1.0, 2.76, 5.40, 8.93, 2.0 }, new[] { 1.0, 0.36, 0.18, 0.07, 0.25 }, new[] { 2.4, 1.0, 0.45, 0.2, 1.4 }, click: 0.02, attack: 0.001); break;
            case Instr.Celesta: Fm(b, start, midi, duration, vel, pan, ratio: 3.5, index: 1.6, decay: 1.6); break;
            case Instr.Flute: Flute(b, start, midi, duration, vel, pan, rng); break;
            case Instr.Pad: Pad(b, start, midi, duration, vel, pan); break;
            case Instr.Bass: Bass(b, start, midi, duration, vel, pan); break;
            case Instr.Pluck: Modal(b, start, midi, vel, pan, rng, new[] { 1.0, 2.0, 3.0, 4.01 }, new[] { 1.0, 0.45, 0.2, 0.1 }, new[] { 0.9, 0.5, 0.3, 0.2 }, click: 0.04, attack: 0.002); break;
        }
    }

    private static double Attack(double t, double a) => t >= a ? 1 : 0.5 - 0.5 * Math.Cos(Math.PI * t / a);

    /// <summary>Soft "felt" piano: slightly inharmonic partials, dual detuned strings, felt-softened attack, damper release.</summary>
    private static void FeltPiano(Buffer b, double start, double midi, double duration, double vel, double pan, Rng rng)
    {
        double f = Note.Hz(midi);
        int s0 = (int)(start * b.Rate);
        double t0 = Math.Clamp(3.2 * Math.Sqrt(220 / f), 0.7, 5.0);         // fundamental decay
        double release = duration + 0.35;                                    // damper
        int n = (int)((release + 0.5) * b.Rate);
        const double B = 0.00035;
        int partials = Math.Min(10, (int)(5500 / f));
        double bright = 0.35 + 0.5 * vel;
        double[] amp = new double[partials], freq = new double[partials], tau = new double[partials], ph = new double[partials];
        for (int k = 1; k <= partials; k++)
        {
            freq[k - 1] = k * f * Math.Sqrt(1 + B * k * k);
            amp[k - 1] = Math.Pow(k, -1.6) * Math.Exp(-(k - 1) * (1.1 - bright));
            tau[k - 1] = t0 / (1 + 0.45 * (k - 1));
            ph[k - 1] = rng.Uniform() * 6.283;
        }
        double detune = Math.Pow(2, 0.9 / 1200);
        var hammer = new Svf(b.Rate);
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / b.Rate;
            double env = Attack(t, 0.006) * (t > duration ? Math.Exp(-(t - duration) / 0.09) : 1);
            if (env < 1e-5 && t > duration) break;
            double v = 0;
            for (int k = 0; k < partials; k++)
            {
                double d = Math.Exp(-t / tau[k]);
                double w = 2 * Math.PI * freq[k] * t + ph[k];
                v += amp[k] * d * (Math.Sin(w) + Math.Sin(w * detune)) * 0.5;
            }
            double thump = t < 0.03 ? hammer.Step(rng.Signed(), 900, 1.2).low * Math.Exp(-t / 0.008) * 0.6 : 0;
            b.Add(s0 + i, (v + thump) * env * vel * 0.42, pan);
        }
    }

    /// <summary>Struck/plucked modal model: partial ratios, amplitudes and decay times (seconds, scaled by pitch).</summary>
    private static void Modal(Buffer b, double start, double midi, double vel, double pan, Rng rng,
        double[] ratios, double[] amps, double[] decays, double click, double attack)
    {
        double f = Note.Hz(midi);
        int s0 = (int)(start * b.Rate);
        double pitchScale = Math.Clamp(Math.Sqrt(440 / f), 0.5, 1.8);
        double longest = decays.Max() * pitchScale * 5;
        int n = (int)(Math.Min(longest, 6) * b.Rate);
        var ph = ratios.Select(_ => rng.Uniform() * 6.283).ToArray();
        var clickFilter = new Svf(b.Rate);
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / b.Rate;
            double v = 0;
            for (int k = 0; k < ratios.Length; k++)
            {
                double fk = f * ratios[k];
                if (fk > b.Rate * 0.45) continue;
                v += amps[k] * Math.Exp(-t / (decays[k] * pitchScale)) * Math.Sin(2 * Math.PI * fk * t + ph[k]);
            }
            if (t < 0.012) v += click * clickFilter.Step(rng.Signed(), Math.Min(6000, f * 4), 0.7).band * Math.Exp(-t / 0.003) * 3;
            b.Add(s0 + i, v * Attack(t, attack) * vel * 0.5, pan);
        }
    }

    /// <summary>Two-operator FM bell (gentle "magical" celesta).</summary>
    private static void Fm(Buffer b, double start, double midi, double duration, double vel, double pan, double ratio, double index, double decay)
    {
        double f = Note.Hz(midi);
        int s0 = (int)(start * b.Rate);
        int n = (int)(Math.Max(duration, decay * 4) * b.Rate);
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / b.Rate;
            double env = Attack(t, 0.002) * Math.Exp(-t / decay);
            double mod = index * Math.Exp(-t / (decay * 0.3)) * Math.Sin(2 * Math.PI * f * ratio * t);
            b.Add(s0 + i, Math.Sin(2 * Math.PI * f * t + mod) * env * vel * 0.38, pan);
        }
    }

    /// <summary>Airy flute/ocarina: near-sine with a soft second partial, breath noise and delayed vibrato.</summary>
    private static void Flute(Buffer b, double start, double midi, double duration, double vel, double pan, Rng rng)
    {
        double f = Note.Hz(midi);
        int s0 = (int)(start * b.Rate);
        int n = (int)((duration + 0.4) * b.Rate);
        var breath = new Svf(b.Rate);
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / b.Rate;
            double env = Attack(t, 0.09) * (t > duration ? Math.Exp(-(t - duration) / 0.12) : 1);
            double vib = 1 + 0.0035 * Math.Sin(2 * Math.PI * 5.0 * t) * Math.Clamp((t - 0.25) / 0.3, 0, 1);
            phase += 2 * Math.PI * f * vib / b.Rate;
            double v = Math.Sin(phase) + 0.18 * Math.Sin(2 * phase) + 0.05 * Math.Sin(3 * phase);
            double air = breath.Step(rng.Signed(), f * 2, 0.5).band * 0.12;
            b.Add(s0 + i, (v + air) * env * vel * 0.30, pan);
        }
    }

    /// <summary>Warm detuned pad: band-limited additive saw, three voices, slow swell.</summary>
    private static void Pad(Buffer b, double start, double midi, double duration, double vel, double pan)
    {
        double f = Note.Hz(midi);
        int s0 = (int)(start * b.Rate);
        int n = (int)((duration + 1.2) * b.Rate);
        double[] det = { 1.0, Math.Pow(2, 6 / 1200.0), Math.Pow(2, -7 / 1200.0) };
        int harm = Math.Max(1, Math.Min(10, (int)(2400 / f)));
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / b.Rate;
            double env = Math.Min(1, t / 0.7) * (t > duration ? Math.Exp(-(t - duration) / 0.45) : 1);
            double v = 0;
            for (int d = 0; d < 3; d++)
                for (int k = 1; k <= harm; k++)
                {
                    double roll = 1.0 / (k * (1 + 0.15 * k * k * f / 900));
                    v += roll * Math.Sin(2 * Math.PI * k * f * det[d] * t + d * 1.7 + k);
                }
            b.Add(s0 + i, v * env * vel * 0.07, pan + 0.25 * Math.Sin(t * 0.7 + midi));
        }
    }

    /// <summary>Round plucked bass with enough harmonics to be heard on small speakers.</summary>
    private static void Bass(Buffer b, double start, double midi, double duration, double vel, double pan)
    {
        double f = Note.Hz(midi);
        int s0 = (int)(start * b.Rate);
        int n = (int)((duration + 0.3) * b.Rate);
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / b.Rate;
            double env = Attack(t, 0.008) * Math.Exp(-t / 1.1) * (t > duration ? Math.Exp(-(t - duration) / 0.08) : 1);
            double w = 2 * Math.PI * f * t;
            double v = Math.Sin(w) + 0.45 * Math.Sin(2 * w) * Math.Exp(-t / 0.4) + 0.2 * Math.Sin(3 * w) * Math.Exp(-t / 0.25);
            b.Add(s0 + i, v * env * vel * 0.34, pan);
        }
    }
}
