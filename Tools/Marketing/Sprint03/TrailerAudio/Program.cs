// PLOP! Sprint 03 trailer audio.
//   dotnet run -c Release -- score <spec.json> <out.wav>   new arrangement of the original PLOP! motif (game synth, unchanged)
//   dotnet run -c Release -- mix   <mix.json>  <out.wav>   music + game SFX at exact times, ducking, loudness-normalised master + stems
//   dotnet run -c Release -- loud  <file.wav>              integrated loudness (BS.1770 K-weighted, gated) and sample peak
// The melody, counter-line and chords are read from Tools/AudioSynth/Score.cs (single source of truth for the motif).
using System.Reflection;
using System.Text.Json;
using PlopAudioSynth;
using Buffer = PlopAudioSynth.Buffer;

switch (args.Length > 0 ? args[0] : "")
{
    case "score": Scorer.Render(args[1], args[2]); break;
    case "mix": Mixer.Run(args[1], args[2]); break;
    case "loud": { var b = Wav.Read(args[1]); Console.WriteLine($"{Path.GetFileName(args[1])}: {b.Length / (double)b.Rate:0.00}s  {Loudness.Integrated(b):0.0} LUFS  peak {20 * Math.Log10(b.Peak()):0.0} dBFS"); break; }
    default: Console.Error.WriteLine("usage: score|mix|loud ..."); return 1;
}
return 0;

static class Scorer
{
    record Section(string type, int bars, int? melodyBar);
    record Spec(double bpm, int transpose, uint seed, List<Section> sections, double tail, double masterRmsDb);

    static T Field<T>(string name) => (T)typeof(Score).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    public static void Render(string specPath, string outPath)
    {
        var spec = JsonSerializer.Deserialize<Spec>(File.ReadAllText(specPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var melody = Field<(int bar, double[] notes)[]>("Melody");
        var counter = Field<(int bar, double[] notes)[]>("CounterLine");
        var chords = Field<(int root, int[] triad)[]>("Chords");
        const int rate = 44100;
        double spb = 60.0 / spec.bpm;
        int totalBars = spec.sections.Sum(s => s.bars);
        var buf = new Buffer(rate, totalBars * 4 * spb + spec.tail + 1);
        var rng = new Rng(spec.seed);
        int tr = spec.transpose;
        int outBar = 0, mbar = 0;
        void Note(Instr i, double beat, double midi, double beats, double vel, double pan) =>
            Instruments.Play(buf, i, beat * spb, midi, beats * spb, vel, pan, spec.seed + (uint)(beat * 64) + (uint)(midi * 3));

        foreach (var sec in spec.sections)
        {
            if (sec.melodyBar is int mb) mbar = mb;
            for (int k = 0; k < sec.bars; k++, outBar++, mbar = (mbar + 1) % 16)
            {
                double b0 = outBar * 4;
                bool last = k == sec.bars - 1;
                var (root, triad) = chords[mbar];
                int r = root + tr; while (r < 50) r += 12; while (r > 61) r -= 12;
                var mel = melody.First(m => m.bar == mbar).notes;

                // ---- per-section palette (all instruments are the game's own) ----
                (Instr lead, double leadVel, int leadOct)? leadPart = sec.type switch
                {
                    "intro" => (Instr.Celesta, 0.42, 0),
                    "build" => (Instr.FeltPiano, 0.55, 0),
                    "montage" => (Instr.Flute, 0.5, 0),
                    "drive" => (Instr.Marimba, 0.58, 0),
                    "outro" => (Instr.FeltPiano, 0.55, 0),
                    "mascot" => (Instr.MusicBox, 0.55, 0),
                    _ => null, // think / ring: no melody
                };
                if (leadPart is var (lead, lv, lo))
                {
                    double beat = b0;
                    for (int i = 0; i < mel.Length; i += 2)
                    {
                        if (mel[i] > 0) Note(lead, beat + rng.Signed() * 0.012, mel[i] + tr + 12 * lo, mel[i + 1] * 0.95, lv * (beat % 4 == 0 ? 1 : 0.88) * rng.Range(0.92, 1), rng.Range(-0.08, 0.08));
                        beat += mel[i + 1];
                    }
                }
                if (sec.type is "montage" or "outro")
                    foreach (var (cb, notes) in counter.Where(c => c.bar == mbar))
                    {
                        double beat = b0;
                        for (int i = 0; i < notes.Length; i += 2) { if (notes[i] > 0) Note(Instr.Celesta, beat, notes[i] + tr, notes[i + 1] * 0.9, 0.22, 0.45); beat += notes[i + 1]; }
                    }

                // Pad
                if (sec.type is "intro" or "think" or "build" or "outro" or "ring" or "mascot")
                    foreach (int iv in triad) Note(Instr.Pad, b0, r + iv + 12, sec.type == "ring" ? 7 : 4, sec.type == "think" ? 0.36 : 0.42, iv == 0 ? 0 : (iv == 7 ? 0.35 : -0.35));
                // Bass
                if (sec.type is "build" or "montage" or "drive" or "outro")
                {
                    Note(Instr.Bass, b0, r - 12, 1.8, 0.62, 0);
                    Note(Instr.Bass, b0 + 2, r - 12 + (outBar % 2 == 0 ? 7 : 0), 1.6, 0.52, 0);
                    if (sec.type == "drive") { Note(Instr.Bass, b0 + 1.5, r - 12, 0.4, 0.42, 0); Note(Instr.Bass, b0 + 3.5, r - 12 + 7, 0.4, 0.4, 0); }
                }
                // Arpeggio
                int[] tones = { r + triad[0], r + triad[1], r + triad[2], r + 12 };
                int[] order = { 0, 2, 1, 3, 2, 1, 3, 2 };
                (Instr arp, double[] pat, int oct, double vel)? arpPart = sec.type switch
                {
                    "intro" => (Instr.MusicBox, new double[] { 0, 1, 2, 3 }, 1, 0.26),
                    "think" => (Instr.MusicBox, new double[] { 0, 1.5, 3 }, 1, 0.22),
                    "build" => (Instr.Kalimba, new double[] { 0, 0.5, 1, 1.5, 2, 2.5, 3, 3.5 }, 0, 0.30),
                    "montage" => (Instr.Marimba, new double[] { 0, 0.5, 1.5, 2, 2.5, 3.5 }, 0, 0.34),
                    "drive" => (Instr.Kalimba, new double[] { 0, 0.5, 1, 1.5, 2, 2.5, 3, 3.5 }, 1, 0.30),
                    "outro" => (Instr.MusicBox, new double[] { 0, 1, 2, 3 }, 1, 0.22),
                    "mascot" => (Instr.Kalimba, new double[] { 0, 0.5, 1.5, 2, 2.5, 3.5 }, 0, 0.28),
                    _ => null,
                };
                if (arpPart is var (arp, pat, ao, av))
                    for (int i = 0; i < pat.Length; i++)
                        Note(arp, b0 + pat[i] + rng.Signed() * 0.008, tones[order[i % 8]] + 12 * ao, 0.45, (pat[i] % 1 == 0 ? av : av * 0.8) * rng.Range(0.85, 1), i % 2 == 0 ? -0.4 : 0.4);
                // Soft woodblock-like pulse (muted low pluck) for the rhythmic sections
                if (sec.type is "montage" or "drive")
                    for (int q = 0; q < 4; q++) Note(Instr.Pluck, b0 + q + 0.5, r + 24, 0.12, q % 2 == 1 ? 0.22 : 0.16, q % 2 == 0 ? 0.3 : -0.3);
                // Sparkle every second bar and on section ends
                if ((outBar % 2 == 1 || last) && sec.type != "ring")
                {
                    int top = root + tr + triad[2]; while (top < 84) top += 12;
                    Note(Instr.Bells, b0 + 3, top, 0.5, 0.2, 0.6); Note(Instr.Bells, b0 + 3.5, top - 3, 0.5, 0.16, -0.6);
                }
                // Ring: a final sustained tonic voicing with a low piano and bells
                if (sec.type == "ring")
                {
                    Note(Instr.FeltPiano, b0, r - 12, 6, 0.5, 0); Note(Instr.FeltPiano, b0 + 0.02, r + triad[1], 6, 0.4, -0.2);
                    Note(Instr.Bells, b0 + 0.5, r + 24 + triad[2], 2, 0.22, 0.4);
                }
            }
        }
        new Reverb(rate, 0.84, 0.38).Process(buf, 0.32, 1);
        buf = buf.Slice(0, (int)Math.Min(buf.Length, (totalBars * 4 * spb + spec.tail) * rate));
        buf.FadeOut(Math.Min(spec.tail, 2.5));
        buf.Master(spec.masterRmsDb == 0 ? -20 : spec.masterRmsDb, -1);
        buf.WriteWav(outPath, false, 24);
        Console.WriteLine($"score {Path.GetFileName(outPath)}: {totalBars} bars @ {spec.bpm} BPM = {totalBars * 4 * spb:0.00}s + tail {spec.tail}s");
    }
}

static class Mixer
{
    public record Track(string file, double at, double @in, double? len, double gainDb, double fadeIn, double fadeOut, string stem);
    public record Cue(string id, double at, double gainDb, double pan);
    public record Duck(double from, double to, double db);
    public record Spec(double duration, string sfxDir, List<Track> tracks, List<Cue> cues, List<Duck> ducks, double targetLufs, double ceilingDb, string stemsDir);

    public static void Run(string specPath, string outPath)
    {
        var s = JsonSerializer.Deserialize<Spec>(File.ReadAllText(specPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        const int rate = 44100;
        var stems = new Dictionary<string, Buffer>();
        Buffer Stem(string n) => stems.TryGetValue(n, out var b) ? b : stems[n] = new Buffer(rate, s.duration);
        foreach (var t in s.tracks ?? new())
        {
            var src = Wav.Read(t.file, rate);
            int i0 = (int)(t.@in * rate), n = (int)((t.len ?? (src.Length / (double)rate - t.@in)) * rate);
            n = Math.Min(n, src.Length - i0);
            var seg = src.Slice(i0, n);
            if (t.fadeIn > 0) seg.FadeIn(t.fadeIn);
            if (t.fadeOut > 0) seg.FadeOut(t.fadeOut);
            Stem(t.stem ?? "music").Mix(seg, Math.Pow(10, t.gainDb / 20), (int)(t.at * rate));
        }
        // Ducking (music stem only), smoothed 120 ms ramps.
        if (stems.TryGetValue("music", out var mus))
            foreach (var d in s.ducks ?? new())
            {
                double g = Math.Pow(10, d.db / 20), ramp = 0.12;
                for (int i = (int)((d.from - ramp) * rate); i < (int)((d.to + ramp) * rate); i++)
                {
                    if (i < 0 || i >= mus.Length) continue;
                    double t = i / (double)rate, k = t < d.from ? (t - (d.from - ramp)) / ramp : t > d.to ? 1 - (t - d.to) / ramp : 1;
                    k = Math.Clamp(k, 0, 1); double gg = 1 - k * (1 - g);
                    mus.L[i] *= (float)gg; mus.R[i] *= (float)gg;
                }
            }
        foreach (var c in s.cues ?? new())
        {
            var src = Wav.Read(Path.Combine(s.sfxDir, c.id.EndsWith(".wav") ? c.id : $"PLOP_Sfx_{c.id}.wav"), rate);
            var sfx = Stem("sfx"); int off = (int)(c.at * rate); double g = Math.Pow(10, c.gainDb / 20);
            for (int i = 0; i < src.Length && off + i < sfx.Length; i++)
            {
                if (off + i < 0) continue;
                double m = 0.5 * (src.L[i] + src.R[i]);
                sfx.L[off + i] += (float)(m * g * Math.Min(1, 1 - c.pan)); sfx.R[off + i] += (float)(m * g * Math.Min(1, 1 + c.pan));
            }
        }
        var master = new Buffer(rate, s.duration);
        foreach (var b in stems.Values) master.Mix(b);
        double lufs = Loudness.Integrated(master), gain = Math.Pow(10, (s.targetLufs - lufs) / 20);
        master.Scale(gain);
        master.SoftLimit(Math.Pow(10, s.ceilingDb / 20));
        master.WriteWav(outPath, false, 24);
        if (!string.IsNullOrEmpty(s.stemsDir))
            foreach (var (n, b) in stems) { b.Scale(gain); b.WriteWav(Path.Combine(s.stemsDir, Path.GetFileNameWithoutExtension(outPath) + "_stem_" + n + ".wav"), false, 24); }
        Console.WriteLine($"mix {Path.GetFileName(outPath)}: {s.duration:0.00}s  pre {lufs:0.0} LUFS -> {Loudness.Integrated(master):0.0} LUFS  peak {20 * Math.Log10(master.Peak()):0.0} dBFS  stems {string.Join(",", stems.Keys)}");
    }
}

static class Wav
{
    public static Buffer Read(string path, int targetRate = 0)
    {
        using var r = new BinaryReader(File.OpenRead(path));
        r.ReadBytes(12);
        int ch = 2, rate = 44100, bits = 16, fmt = 1;
        while (r.BaseStream.Position < r.BaseStream.Length)
        {
            string id = new string(r.ReadChars(4)); int size = r.ReadInt32();
            if (id == "fmt ") { fmt = r.ReadInt16(); ch = r.ReadInt16(); rate = r.ReadInt32(); r.ReadInt32(); r.ReadInt16(); bits = r.ReadInt16(); r.ReadBytes(size - 16); }
            else if (id == "data")
            {
                int bps = bits / 8, frames = size / (bps * ch);
                var b = new Buffer(rate, frames);
                for (int i = 0; i < frames; i++)
                    for (int c = 0; c < ch; c++)
                    {
                        double v = bits switch
                        {
                            16 => r.ReadInt16() / 32768.0,
                            24 => ((r.ReadByte() | (r.ReadByte() << 8) | (r.ReadSByte() << 16))) / 8388608.0,
                            32 when fmt == 3 => r.ReadSingle(),
                            _ => r.ReadInt32() / 2147483648.0,
                        };
                        if (ch == 1) { b.L[i] = b.R[i] = (float)v; } else if (c == 0) b.L[i] = (float)v; else if (c == 1) b.R[i] = (float)v;
                    }
                return targetRate == 0 || targetRate == rate ? b : Resample(b, targetRate);
            }
            else r.ReadBytes(size + (size & 1));
        }
        throw new InvalidDataException("no data chunk: " + path);
    }

    static Buffer Resample(Buffer s, int rate)
    {
        int n = (int)((long)s.Length * rate / s.Rate); var o = new Buffer(rate, n); double k = s.Rate / (double)rate;
        for (int i = 0; i < n; i++) { double p = i * k; int a = (int)p; double f = p - a; int b2 = Math.Min(a + 1, s.Length - 1);
            o.L[i] = (float)(s.L[a] * (1 - f) + s.L[b2] * f); o.R[i] = (float)(s.R[a] * (1 - f) + s.R[b2] * f); }
        return o;
    }
}

static class Loudness
{
    // ITU-R BS.1770-4: K-weighting (pre-filter shelf + RLB high-pass, coefficients derived for any rate), 400 ms blocks,
    // 75 % overlap, absolute gate -70 LUFS, relative gate -10 LU.
    public static double Integrated(Buffer b)
    {
        var (sb, sa) = Shelf(b.Rate); var (hb, ha) = HighPass(b.Rate);
        double[] l = Filter(Filter(b.L, sb, sa), hb, ha), r = Filter(Filter(b.R, sb, sa), hb, ha);
        int block = (int)(0.4 * b.Rate), hop = block / 4; var z = new List<double>();
        for (int s = 0; s + block <= b.Length; s += hop) { double acc = 0; for (int i = s; i < s + block; i++) acc += l[i] * l[i] + r[i] * r[i]; z.Add(acc / block); }
        double L(double ms) => -0.691 + 10 * Math.Log10(Math.Max(ms, 1e-12));
        var g1 = z.Where(m => L(m) > -70).ToList(); if (g1.Count == 0) return -70;
        double rel = L(g1.Average()) - 10; var g2 = g1.Where(m => L(m) > rel).ToList();
        return L(g2.Average());
    }
    static double[] Filter(IReadOnlyList<float> x, double[] b, double[] a) => Filter(x.Select(v => (double)v).ToArray(), b, a);
    static double[] Filter(double[] x, double[] b, double[] a)
    {
        var y = new double[x.Length]; double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
        for (int i = 0; i < x.Length; i++) { double v = b[0] * x[i] + b[1] * x1 + b[2] * x2 - a[1] * y1 - a[2] * y2; x2 = x1; x1 = x[i]; y2 = y1; y1 = v; y[i] = v; }
        return y;
    }
    static (double[], double[]) Shelf(int fs)
    {
        double G = 3.999843853973347, Q = 0.7071752369554196, fc = 1681.974450955533;
        double K = Math.Tan(Math.PI * fc / fs), Vh = Math.Pow(10, G / 20), Vb = Math.Pow(Vh, 0.4996667741545416), a0 = 1 + K / Q + K * K;
        return (new[] { (Vh + Vb * K / Q + K * K) / a0, 2 * (K * K - Vh) / a0, (Vh - Vb * K / Q + K * K) / a0 }, new[] { 1, 2 * (K * K - 1) / a0, (1 - K / Q + K * K) / a0 });
    }
    static (double[], double[]) HighPass(int fs)
    {
        double Q = 0.5003270373238773, fc = 38.13547087602444, K = Math.Tan(Math.PI * fc / fs), a0 = 1 + K / Q + K * K;
        return (new[] { 1.0, -2.0, 1.0 }, new[] { 1, 2 * (K * K - 1) / a0, (1 - K / Q + K * K) / a0 });
    }
}
