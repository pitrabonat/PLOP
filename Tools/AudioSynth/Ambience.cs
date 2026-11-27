// PLOP! offline audio synthesis — World ambience beds (procedural noise + sparse events, seamless loops).
namespace PlopAudioSynth;

public static class Ambience
{
    public sealed record Bed(string Id, string Description);

    public static readonly Bed[] Beds =
    {
        new("cozy-nest", "warm room tone, soft hearth crackle"),
        new("dewdrop-garden", "light breeze, water drips, distant small birds"),
        new("rainy-window", "gentle rain on glass, soft droplets, low room tone"),
        new("twilight-attic", "quiet room tone, crickets, occasional soft wood creak"),
        new("moonlit-clouds", "airy high wind, slow shimmer tones"),
        new("frosted-pond", "cold breeze, sparse ice tinkles"),
        new("breezy-meadow", "wind gusts, grass rustle, birds"),
        new("ember-hollow", "warm low rumble, lava bubbles, embers crackling"),
        new("prism-observatory", "soft glassy hum, prismatic chimes, air"),
    };

    public const double LoopSeconds = 30;

    public static Buffer Render(string id, int rate, uint seed)
    {
        int n = (int)(LoopSeconds * rate);
        const double xfade = 3.0;
        int xf = (int)(xfade * rate);
        // Continuous bed rendered with xfade extra seconds, then folded into a loop.
        var bed = new Buffer(rate, n + xf);
        // Events rendered twice (t and t + loop) and the second loop kept, so they wrap seamlessly.
        var events = new Buffer(rate, 2 * n);
        var rng = new Rng(seed);

        switch (id)
        {
            case "cozy-nest":
                Noise(bed, rng, 380, 0.7, 0.22, 0.05, 0.11);
                Noise(bed, rng, 1200, 1.5, 0.05, 0.08, 0.37);
                Scatter(events, rng, n, 55, (b, t, r) => Crackle(b, t, r, 0.18));
                Scatter(events, rng, n, 6, (b, t, r) => Pop(b, t, r, 0.22));
                break;
            case "dewdrop-garden":
                Noise(bed, rng, 900, 1.3, 0.10, 0.07, 0.13);
                Scatter(events, rng, n, 16, (b, t, r) => Drip(b, t, r, 0.30));
                Scatter(events, rng, n, 5, (b, t, r) => Bird(b, t, r, 0.16));
                break;
            case "rainy-window":
                Noise(bed, rng, 3200, 1.0, 0.20, 0.03, 0.09);
                Noise(bed, rng, 420, 0.8, 0.10, 0.04, 0.05);
                Scatter(events, rng, n, 420, (b, t, r) => Droplet(b, t, r, 0.10));
                Scatter(events, rng, n, 12, (b, t, r) => Drip(b, t, r, 0.16));
                break;
            case "twilight-attic":
                Noise(bed, rng, 500, 0.9, 0.12, 0.04, 0.07);
                Scatter(events, rng, n, 9, (b, t, r) => Cricket(b, t, r, 0.10));
                Scatter(events, rng, n, 3, (b, t, r) => Creak(b, t, r, 0.20));
                break;
            case "moonlit-clouds":
                Noise(bed, rng, 1500, 1.6, 0.16, 0.09, 0.07);
                Scatter(events, rng, n, 7, (b, t, r) => Shimmer(b, t, r, 0.10, 2400, 4.0));
                break;
            case "frosted-pond":
                Noise(bed, rng, 1700, 1.4, 0.16, 0.11, 0.09);
                Noise(bed, rng, 600, 1.0, 0.07, 0.05, 0.21);
                Scatter(events, rng, n, 14, (b, t, r) => Tinkle(b, t, r, 0.16));
                break;
            case "breezy-meadow":
                Noise(bed, rng, 1100, 1.2, 0.20, 0.16, 0.08);
                Scatter(events, rng, n, 10, (b, t, r) => Rustle(b, t, r, 0.18));
                Scatter(events, rng, n, 9, (b, t, r) => Bird(b, t, r, 0.18));
                break;
            case "ember-hollow":
                Noise(bed, rng, 320, 0.8, 0.26, 0.08, 0.06);
                Noise(bed, rng, 700, 1.3, 0.06, 0.05, 0.17);
                Scatter(events, rng, n, 14, (b, t, r) => Bubble(b, t, r, 0.30));
                Scatter(events, rng, n, 60, (b, t, r) => Crackle(b, t, r, 0.14));
                break;
            case "prism-observatory":
                Drone(bed, new[] { 57.0, 64.0, 69.0, 76.0 }, 0.05);
                Noise(bed, rng, 2200, 1.5, 0.07, 0.05, 0.05);
                Scatter(events, rng, n, 8, (b, t, r) => Shimmer(b, t, r, 0.11, 1760, 3.0));
                Scatter(events, rng, n, 6, (b, t, r) => Tinkle(b, t, r, 0.10));
                break;
            default: throw new ArgumentException("unknown ambience " + id);
        }

        var outBuf = new Buffer(rate, n);
        // Fold the bed: equal-power crossfade of the extra tail into the head.
        for (int i = 0; i < n; i++)
        {
            double l = bed.L[i], r = bed.R[i];
            if (i < xf)
            {
                double x = (double)i / xf;
                double gin = Math.Sin(0.5 * Math.PI * x), gout = Math.Cos(0.5 * Math.PI * x);
                l = l * gin + bed.L[n + i] * gout;
                r = r * gin + bed.R[n + i] * gout;
            }
            outBuf.L[i] = (float)(l + events.L[n + i]);
            outBuf.R[i] = (float)(r + events.R[n + i]);
        }
        return Looped.Reverb(outBuf, 0.7, 0.45, 0.18);
    }

    // ------------------------------------------------------------------ beds

    /// <summary>Band-passed noise whose level breathes slowly (modulation depth, rate Hz).</summary>
    private static void Noise(Buffer b, Rng rng, double centre, double q, double level, double depth, double rateHz)
    {
        var fl = new Svf(b.Rate); var fr = new Svf(b.Rate);
        double ph = rng.Uniform() * 6.283, ph2 = rng.Uniform() * 6.283;
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / b.Rate;
            double mod = 1 + depth / Math.Max(level, 1e-6) * (0.6 * Math.Sin(2 * Math.PI * rateHz * t + ph) + 0.4 * Math.Sin(2 * Math.PI * rateHz * 2.71 * t + ph2));
            double c = centre * (1 + 0.12 * Math.Sin(2 * Math.PI * rateHz * 0.5 * t + ph2));
            double l = fl.Step(rng.Signed(), c, q).band, r = fr.Step(rng.Signed(), c, q).band;
            b.L[i] += (float)(l * level * Math.Max(0.1, mod));
            b.R[i] += (float)(r * level * Math.Max(0.1, mod));
        }
    }

    private static void Drone(Buffer b, double[] midis, double level)
    {
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / b.Rate, v = 0;
            for (int k = 0; k < midis.Length; k++)
            {
                double f = Note.Hz(midis[k]);
                v += Math.Sin(2 * Math.PI * f * t + k) * (0.6 + 0.4 * Math.Sin(2 * Math.PI * 0.05 * (k + 1) * t)) / (k + 1);
            }
            b.L[i] += (float)(v * level);
            b.R[i] += (float)(v * level * 0.95);
        }
    }

    // ------------------------------------------------------------------ events

    private delegate void Event(Buffer b, double t, Rng rng);

    /// <summary>Places <paramref name="count"/> events at random times in one loop, rendering each twice for the wrap.</summary>
    private static void Scatter(Buffer b, Rng rng, int loopFrames, int count, Event e)
    {
        double loop = (double)loopFrames / b.Rate;
        for (int i = 0; i < count; i++)
        {
            double t = rng.Uniform() * loop;
            uint s = rng.Next();
            e(b, t, new Rng(s));
            e(b, t + loop, new Rng(s));
        }
    }

    private static void Crackle(Buffer b, double t, Rng r, double gain)
    {
        int s0 = (int)(t * b.Rate), n = (int)(0.012 * b.Rate);
        var f = new Svf(b.Rate); double c = r.Range(1800, 4200), pan = r.Signed() * 0.6, g = gain * r.Range(0.3, 1);
        for (int i = 0; i < n; i++) b.Add(s0 + i, f.Step(r.Signed(), c, 0.6).band * Math.Exp(-i / (0.002 * b.Rate)) * g * 2.5, pan);
    }

    private static void Pop(Buffer b, double t, Rng r, double gain)
    {
        int s0 = (int)(t * b.Rate), n = (int)(0.05 * b.Rate);
        double f0 = r.Range(500, 900), pan = r.Signed() * 0.5;
        for (int i = 0; i < n; i++) { double tt = (double)i / b.Rate; b.Add(s0 + i, Math.Sin(2 * Math.PI * f0 * tt * (1 - 2 * tt)) * Math.Exp(-tt / 0.012) * gain, pan); }
    }

    private static void Drip(Buffer b, double t, Rng r, double gain)
    {
        // water drop: a short sine whose pitch rises quickly (bubble resonance)
        int s0 = (int)(t * b.Rate), n = (int)(0.09 * b.Rate);
        double f0 = r.Range(1100, 2100), pan = r.Signed() * 0.7, g = gain * r.Range(0.5, 1), ph = 0;
        for (int i = 0; i < n; i++)
        {
            double tt = (double)i / b.Rate;
            double f = f0 * (1 + 0.9 * (1 - Math.Exp(-tt / 0.018)));
            ph += 2 * Math.PI * f / b.Rate;
            b.Add(s0 + i, Math.Sin(ph) * Math.Exp(-tt / 0.022) * Math.Min(1, tt / 0.001) * g, pan);
        }
    }

    private static void Droplet(Buffer b, double t, Rng r, double gain)
    {
        int s0 = (int)(t * b.Rate), n = (int)(0.02 * b.Rate);
        var f = new Svf(b.Rate); double c = r.Range(2500, 6000), pan = r.Signed() * 0.9, g = gain * r.Range(0.2, 1);
        for (int i = 0; i < n; i++) b.Add(s0 + i, f.Step(r.Signed(), c, 0.3).band * Math.Exp(-i / (0.003 * b.Rate)) * g * 2, pan);
    }

    private static void Bird(Buffer b, double t, Rng r, double gain)
    {
        // 2–5 quick FM chirps
        int chirps = 2 + (int)(r.Uniform() * 4);
        double pan = r.Signed() * 0.8, f0 = r.Range(2600, 4200);
        for (int c = 0; c < chirps; c++)
        {
            double start = t + c * r.Range(0.09, 0.15), len = r.Range(0.05, 0.09);
            int s0 = (int)(start * b.Rate), n = (int)(len * b.Rate);
            double ph = 0, dir = r.Uniform() < 0.6 ? 1 : -1;
            for (int i = 0; i < n; i++)
            {
                double x = (double)i / n;
                double f = f0 * (1 + dir * 0.35 * x) + 220 * Math.Sin(2 * Math.PI * 38 * x * len);
                ph += 2 * Math.PI * f / b.Rate;
                b.Add(s0 + i, Math.Sin(ph) * Math.Sin(Math.PI * x) * gain, pan);
            }
        }
    }

    private static void Cricket(Buffer b, double t, Rng r, double gain)
    {
        // a short train of chirps at ~4.4 kHz, each chirp of 3 pulses
        double f = r.Range(4200, 4700), pan = r.Signed() * 0.8;
        int trains = 3 + (int)(r.Uniform() * 4);
        for (int k = 0; k < trains; k++)
            for (int p = 0; p < 3; p++)
            {
                double start = t + k * 0.32 + p * 0.022;
                int s0 = (int)(start * b.Rate), n = (int)(0.014 * b.Rate);
                for (int i = 0; i < n; i++) { double x = (double)i / n; b.Add(s0 + i, Math.Sin(2 * Math.PI * f * i / b.Rate) * Math.Sin(Math.PI * x) * gain, pan); }
            }
    }

    private static void Creak(Buffer b, double t, Rng r, double gain)
    {
        int s0 = (int)(t * b.Rate); double len = r.Range(0.4, 0.8); int n = (int)(len * b.Rate);
        var f = new Svf(b.Rate); double c0 = r.Range(300, 500), pan = r.Signed() * 0.5;
        for (int i = 0; i < n; i++)
        {
            double x = (double)i / n;
            // stick-slip: pulse train with jittering rate, resonated
            double pulse = (i % (int)(b.Rate / (60 + 40 * Math.Sin(x * 9)))) == 0 ? 1 : 0;
            b.Add(s0 + i, f.Step(pulse + 0.02 * r.Signed(), c0 * (1 + 0.3 * x), 0.15).band * Math.Sin(Math.PI * x) * gain, pan);
        }
    }

    private static void Shimmer(Buffer b, double t, Rng r, double gain, double baseHz, double len)
    {
        int s0 = (int)(t * b.Rate), n = (int)(len * b.Rate);
        double[] ratios = { 1, 1.5, 2, 2.5 };
        double f = baseHz * Math.Pow(2, Math.Round(r.Range(0, 5)) / 12.0), pan = r.Signed() * 0.7;
        for (int i = 0; i < n; i++)
        {
            double x = (double)i / n, tt = (double)i / b.Rate, v = 0;
            for (int k = 0; k < ratios.Length; k++) v += Math.Sin(2 * Math.PI * f * ratios[k] * tt + k) / (k + 1);
            b.Add(s0 + i, v * Math.Sin(Math.PI * x) * Math.Sin(Math.PI * x) * gain * 0.5, pan * Math.Cos(Math.PI * x));
        }
    }

    private static void Tinkle(Buffer b, double t, Rng r, double gain)
    {
        int s0 = (int)(t * b.Rate), n = (int)(1.2 * b.Rate);
        double f = r.Range(2800, 5200), pan = r.Signed() * 0.8, g = gain * r.Range(0.4, 1);
        for (int i = 0; i < n; i++)
        {
            double tt = (double)i / b.Rate;
            double v = Math.Sin(2 * Math.PI * f * tt) * Math.Exp(-tt / 0.25) + 0.4 * Math.Sin(2 * Math.PI * f * 2.76 * tt) * Math.Exp(-tt / 0.08);
            b.Add(s0 + i, v * Math.Min(1, tt / 0.0008) * g, pan);
        }
    }

    private static void Rustle(Buffer b, double t, Rng r, double gain)
    {
        int s0 = (int)(t * b.Rate); double len = r.Range(0.6, 1.4); int n = (int)(len * b.Rate);
        var f = new Svf(b.Rate); double pan = r.Signed() * 0.8;
        for (int i = 0; i < n; i++)
        {
            double x = (double)i / n;
            double grain = r.Uniform() < 0.25 ? r.Signed() : 0;
            b.Add(s0 + i, f.Step(grain, 3200, 0.5).band * Math.Sin(Math.PI * x) * gain * 1.6, pan);
        }
    }

    private static void Bubble(Buffer b, double t, Rng r, double gain)
    {
        // a thick lava "blub": low pitched bubble rising in pitch, with harmonics so it reads on small speakers
        int s0 = (int)(t * b.Rate), n = (int)(0.22 * b.Rate);
        double f0 = r.Range(160, 300), pan = r.Signed() * 0.6, g = gain * r.Range(0.5, 1), ph = 0;
        for (int i = 0; i < n; i++)
        {
            double tt = (double)i / b.Rate;
            double f = f0 * (1 + 1.2 * (1 - Math.Exp(-tt / 0.05)));
            ph += 2 * Math.PI * f / b.Rate;
            double v = Math.Sin(ph) + 0.5 * Math.Sin(2 * ph) + 0.25 * Math.Sin(3 * ph);
            b.Add(s0 + i, v * Math.Exp(-tt / 0.06) * Math.Min(1, tt / 0.004) * g * 0.6, pan);
        }
    }
}
