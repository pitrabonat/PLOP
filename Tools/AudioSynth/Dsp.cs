// PLOP! offline audio synthesis — DSP building blocks. Original code; no samples, no third-party audio.
namespace PlopAudioSynth;

/// <summary>Stereo float buffer with mixing helpers.</summary>
public sealed class Buffer
{
    public readonly int Rate;
    public float[] L, R;
    public int Length => L.Length;

    public Buffer(int rate, double seconds) : this(rate, (int)Math.Ceiling(seconds * rate)) { }
    public Buffer(int rate, int frames) { Rate = rate; L = new float[frames]; R = new float[frames]; }

    public void Add(int index, double value, double pan)
    {
        if ((uint)index >= (uint)L.Length) return;
        // equal-power pan, pan in [-1, 1]
        double a = (pan + 1) * Math.PI / 4;
        L[index] += (float)(value * Math.Cos(a));
        R[index] += (float)(value * Math.Sin(a));
    }

    public void Mix(Buffer other, double gain = 1, int offset = 0)
    {
        for (int i = 0; i < other.Length; i++)
        {
            int j = i + offset;
            if ((uint)j >= (uint)L.Length) continue;
            L[j] += (float)(other.L[i] * gain);
            R[j] += (float)(other.R[i] * gain);
        }
    }

    public Buffer Slice(int start, int frames)
    {
        var b = new Buffer(Rate, frames);
        Array.Copy(L, start, b.L, 0, Math.Min(frames, L.Length - start));
        Array.Copy(R, start, b.R, 0, Math.Min(frames, R.Length - start));
        return b;
    }

    public double Peak()
    {
        double p = 0;
        for (int i = 0; i < Length; i++) p = Math.Max(p, Math.Max(Math.Abs(L[i]), Math.Abs(R[i])));
        return p;
    }

    public double Rms()
    {
        double s = 0;
        for (int i = 0; i < Length; i++) s += L[i] * L[i] + R[i] * R[i];
        return Math.Sqrt(s / Math.Max(1, 2 * Length));
    }

    public void Scale(double g)
    {
        for (int i = 0; i < Length; i++) { L[i] *= (float)g; R[i] *= (float)g; }
    }

    /// <summary>Scale to a target RMS (dBFS), then a soft limiter keeps peaks under the ceiling (dBFS).</summary>
    public void Master(double rmsDb, double ceilingDb)
    {
        double rms = Rms();
        if (rms > 0) Scale(Math.Pow(10, rmsDb / 20) / rms);
        SoftLimit(Math.Pow(10, ceilingDb / 20));
    }

    /// <summary>Peak normalisation (short effects), then soft limiting.</summary>
    public void NormalizePeak(double peakDb)
    {
        double p = Peak();
        if (p > 0) Scale(Math.Pow(10, peakDb / 20) / p);
    }

    public void SoftLimit(double ceiling)
    {
        double knee = ceiling * 0.7;
        for (int i = 0; i < Length; i++) { L[i] = Limit(L[i], knee, ceiling); R[i] = Limit(R[i], knee, ceiling); }
    }

    private static float Limit(float x, double knee, double ceiling)
    {
        double a = Math.Abs(x);
        if (a <= knee) return x;
        double over = (a - knee) / (ceiling - knee);
        double y = knee + (ceiling - knee) * Math.Tanh(over);
        return (float)(Math.Sign(x) * y);
    }

    public void FadeIn(double seconds) { int n = (int)(seconds * Rate); for (int i = 0; i < Math.Min(n, Length); i++) { double g = Math.Sin(0.5 * Math.PI * i / n); L[i] *= (float)g; R[i] *= (float)g; } }
    public void FadeOut(double seconds) { int n = (int)(seconds * Rate); for (int i = 0; i < Math.Min(n, Length); i++) { int k = Length - 1 - i; double g = Math.Sin(0.5 * Math.PI * i / n); L[k] *= (float)g; R[k] *= (float)g; } }

    public void ToMono()
    {
        for (int i = 0; i < Length; i++) { float m = 0.5f * (L[i] + R[i]); L[i] = m; R[i] = m; }
    }

    public void WriteWav(string path, bool mono = false, int bits = 16)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        int ch = mono ? 1 : 2, bps = bits / 8;
        using var w = new BinaryWriter(File.Create(path));
        int data = Length * ch * bps;
        w.Write("RIFF"u8); w.Write(36 + data); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)ch); w.Write(Rate); w.Write(Rate * ch * bps); w.Write((short)(ch * bps)); w.Write((short)bits);
        w.Write("data"u8); w.Write(data);
        uint dither = 0x9E3779B9u;
        for (int i = 0; i < Length; i++)
        {
            if (mono) Sample(w, 0.5 * (L[i] + R[i]), bits, ref dither);
            else { Sample(w, L[i], bits, ref dither); Sample(w, R[i], bits, ref dither); }
        }
    }

    private static void Sample(BinaryWriter w, double v, int bits, ref uint seed)
    {
        v = Math.Clamp(v, -1, 1);
        if (bits == 24)
        {
            int s = (int)Math.Round(v * 8388607);
            w.Write((byte)(s & 0xff)); w.Write((byte)((s >> 8) & 0xff)); w.Write((byte)((s >> 16) & 0xff));
        }
        else
        {
            // TPDF dither for 16-bit
            seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; double r1 = (seed & 0xffff) / 65535.0;
            seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; double r2 = (seed & 0xffff) / 65535.0;
            int s = (int)Math.Round(v * 32767 + (r1 - r2));
            w.Write((short)Math.Clamp(s, -32768, 32767));
        }
    }
}

/// <summary>Deterministic noise source (xorshift) so every render is byte-identical.</summary>
public sealed class Rng
{
    private uint _s;
    public Rng(uint seed) { _s = seed == 0 ? 0x1234567u : seed; }
    public uint Next() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return _s; }
    public double Uniform() => Next() / 4294967296.0;
    public double Signed() => Uniform() * 2 - 1;
    public double Range(double a, double b) => a + (b - a) * Uniform();
}

/// <summary>State-variable filter (Chamberlin), used for band/low/high-passed noise.</summary>
public sealed class Svf
{
    private double _low, _band;
    private readonly int _rate;
    public Svf(int rate) { _rate = rate; }
    public (double low, double band, double high) Step(double x, double cutoff, double q)
    {
        double f = 2 * Math.Sin(Math.PI * Math.Min(cutoff, _rate / 6.0) / _rate);
        double high = x - _low - q * _band;
        _band += f * high;
        _low += f * _band;
        return (_low, _band, high);
    }
}

/// <summary>Freeverb-style stereo reverb (8 damped combs + 4 allpasses per side).</summary>
public sealed class Reverb
{
    private sealed class Comb { public float[] B; public int I; public double Store; }
    private sealed class AllPass { public float[] B; public int I; }
    private readonly Comb[] _cl, _cr; private readonly AllPass[] _al, _ar;
    private readonly double _feedback, _damp;

    public Reverb(int rate, double room = 0.82, double damp = 0.35)
    {
        int[] combs = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
        int[] alls = { 556, 441, 341, 225 };
        double s = rate / 44100.0; const int spread = 23;
        _cl = combs.Select(c => new Comb { B = new float[(int)(c * s)] }).ToArray();
        _cr = combs.Select(c => new Comb { B = new float[(int)((c + spread) * s)] }).ToArray();
        _al = alls.Select(a => new AllPass { B = new float[(int)(a * s)] }).ToArray();
        _ar = alls.Select(a => new AllPass { B = new float[(int)((a + spread) * s)] }).ToArray();
        _feedback = room; _damp = damp;
    }

    public void Process(Buffer b, double wet, double dry = 1)
    {
        for (int i = 0; i < b.Length; i++)
        {
            double input = (b.L[i] + b.R[i]) * 0.015;
            double ol = 0, or = 0;
            foreach (var c in _cl) ol += CombStep(c, input);
            foreach (var c in _cr) or += CombStep(c, input);
            foreach (var a in _al) ol = AllStep(a, ol);
            foreach (var a in _ar) or = AllStep(a, or);
            b.L[i] = (float)(b.L[i] * dry + ol * wet);
            b.R[i] = (float)(b.R[i] * dry + or * wet);
        }
    }

    private double CombStep(Comb c, double x)
    {
        double y = c.B[c.I];
        c.Store = y * (1 - _damp) + c.Store * _damp;
        c.B[c.I] = (float)(x + c.Store * _feedback);
        if (++c.I >= c.B.Length) c.I = 0;
        return y;
    }

    private static double AllStep(AllPass a, double x)
    {
        double buf = a.B[a.I];
        double y = -x + buf;
        a.B[a.I] = (float)(x + buf * 0.5);
        if (++a.I >= a.B.Length) a.I = 0;
        return y;
    }
}

public static class Note
{
    public static double Hz(double midi) => 440 * Math.Pow(2, (midi - 69) / 12.0);
}
