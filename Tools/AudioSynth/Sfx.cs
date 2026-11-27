// PLOP! offline audio synthesis — gameplay, UI and reward effects. Tuned for phone speakers: the energy that
// carries each cue sits between ~400 Hz and ~4 kHz, with soft attacks (no harsh transients) and short tails.
namespace PlopAudioSynth;

public static class Looped
{
    /// <summary>Reverb that wraps: the loop is processed twice in a row and the second copy kept.</summary>
    public static Buffer Reverb(Buffer loop, double room, double damp, double wet)
    {
        int n = loop.Length;
        var twice = new Buffer(loop.Rate, 2 * n);
        twice.Mix(loop); twice.Mix(loop, 1, n);
        new PlopAudioSynth.Reverb(loop.Rate, room, damp).Process(twice, wet, 1);
        return twice.Slice(n, n);
    }

    /// <summary>One-shot reverb (effects): processed in place; the buffer length already leaves room for the tail.</summary>
    public static Buffer Tail(Buffer b, double room, double wet)
    {
        new PlopAudioSynth.Reverb(b.Rate, room, 0.4).Process(b, wet, 1);
        return b;
    }
}

public sealed record Cue(string Id, string Use, double PeakDb, Func<int, Buffer> Render);

public static class Sfx
{
    private static double Hz(double m) => Note.Hz(m);

    public static IEnumerable<Cue> All()
    {
        // ---- movement (four variants so repeated moves never sound mechanical)
        double[] movePitch = { 0, 2, -2, 4 };
        for (int i = 0; i < movePitch.Length; i++)
        {
            double p = movePitch[i]; uint s = (uint)(11 + i);
            yield return new($"move_{i + 1}", "a Plop hops one cell", -6, r => Plop(r, 560 * Math.Pow(2, p / 12), 0.16, s));
        }
        yield return new("blocked_1", "move into a wall / nothing moves", -7, r => Bonk(r, 460, 1));
        yield return new("blocked_2", "move into a wall (variant)", -7, r => Bonk(r, 420, 2));
        yield return new("collision_1", "two Plops bump", -6, r => Bump(r, 620, 3));
        yield return new("collision_2", "two Plops bump (variant)", -6, r => Bump(r, 560, 4));
        yield return new("settle", "a Plop lands / stops", -10, r => Settle(r));
        yield return new("sleep", "a Plop reaches its matching Nest and falls asleep", -4, r => Sleep(r));

        // ---- mechanics
        yield return new("ice_slide", "Frost: sliding across ice", -8, r => IceSlide(r));
        yield return new("ice_stop", "Frost: slide comes to rest", -9, r => IceStop(r));
        yield return new("fan", "Breeze fan pushes a Plop", -7, r => Whoosh(r, 0.5, 700, 2200, 5));
        yield return new("portal_in", "Loop portal entry", -6, r => Portal(r, rising: true));
        yield return new("portal_out", "Loop portal exit", -6, r => Portal(r, rising: false));
        yield return new("portal_blocked", "Loop exit is occupied", -8, r => PortalBlocked(r));
        yield return new("lava_sizzle", "Lava: a Plop touches lava (level failed)", -7, r => Sizzle(r));
        yield return new("lava_bubble", "Lava: bubbling accent", -8, r => LavaBubble(r));

        // ---- outcomes and flow
        yield return new("complete", "level complete jingle (motif head)", -3, r => Jingle(r, new[] { 69.0, 72, 77 }, new[] { 0, 0.11, 0.22 }, 1.5, false));
        yield return new("perfect", "Perfect (optimal moves) jingle", -3, r => Jingle(r, new[] { 69.0, 72, 77, 81, 84 }, new[] { 0, 0.1, 0.2, 0.3, 0.42 }, 2.0, true));
        yield return new("deadlock", "no moves left / stuck, gentle", -6, r => Deadlock(r));
        yield return new("undo", "undo one move", -8, r => Undo(r));
        yield return new("restart", "restart the level", -7, r => Restart(r));
        yield return new("ui_tap", "button tap", -9, r => Tap(r, 84));
        yield return new("ui_back", "back / close", -9, r => Tap(r, 79));
        yield return new("ui_toggle", "toggle / slider tick", -12, r => Tap(r, 88, 0.6));
        yield return new("dust", "Dream Dust awarded", -6, r => Dust(r));
        yield return new("cosmetic_unlock", "cosmetic unlocked / equipped", -4, r => CosmeticUnlock(r));
        yield return new("world_unlock", "a new World opens", -3, r => WorldUnlock(r));
        yield return new("sonic_logo", "PLOP! sonic logo (splash, trailer)", -2, r => SonicLogo(r));
    }

    // ------------------------------------------------------------------ building blocks

    private static double Env(double t, double a, double tau) => (t < a ? 0.5 - 0.5 * Math.Cos(Math.PI * t / a) : 1) * Math.Exp(-Math.Max(0, t - a) / tau);

    /// <summary>The signature "plop": a rounded bubble whose pitch rises, with a soft 2nd harmonic and a tiny body thump.</summary>
    private static Buffer Plop(int rate, double f0, double len, uint seed)
    {
        var b = new Buffer(rate, len + 0.05);
        var rng = new Rng(seed);
        double ph = 0;
        var body = new Svf(rate);
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / rate;
            double f = f0 * (1 + 0.62 * (1 - Math.Exp(-t / 0.028)));
            ph += 2 * Math.PI * f / rate;
            double v = Math.Sin(ph) + 0.28 * Math.Sin(2 * ph) * Math.Exp(-t / 0.03) + 0.1 * Math.Sin(3 * ph) * Math.Exp(-t / 0.02);
            double env = Env(t, 0.004, 0.045);
            double thump = t < 0.02 ? body.Step(rng.Signed(), 700, 1.0).low * Math.Exp(-t / 0.006) * 0.8 : 0;
            b.Add(i, (v * env + thump) * 0.6, 0);
        }
        return b;
    }

    private static Buffer Bonk(int rate, double f0, uint seed)
    {
        var b = new Buffer(rate, 0.22);
        var rng = new Rng(seed); var f = new Svf(rate); double ph = 0;
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / rate;
            double fr = f0 * (1 - 0.25 * (1 - Math.Exp(-t / 0.04)));
            ph += 2 * Math.PI * fr / rate;
            double wobble = 1 + 0.25 * Math.Sin(2 * Math.PI * 18 * t) * Math.Exp(-t / 0.05);
            double v = (Math.Sin(ph) + 0.5 * Math.Sin(2 * ph) + 0.25 * Math.Sin(3 * ph)) * Env(t, 0.003, 0.05) * wobble;
            v += f.Step(rng.Signed(), 900, 0.8).band * Math.Exp(-t / 0.01) * 0.6;
            b.Add(i, v * 0.5, 0);
        }
        return b;
    }

    private static Buffer Bump(int rate, double f0, uint seed)
    {
        var b = new Buffer(rate, 0.25);
        b.Mix(Plop(rate, f0, 0.12, seed));
        b.Mix(Plop(rate, f0 * 1.19, 0.12, seed + 1), 0.8, (int)(0.055 * rate));
        // tiny squeak: two plops squish together
        for (int i = 0; i < (int)(0.06 * rate); i++)
        {
            double t = (double)i / rate;
            b.Add(i + (int)(0.03 * rate), Math.Sin(2 * Math.PI * (1500 + 900 * t / 0.06) * t) * Math.Sin(Math.PI * t / 0.06) * 0.15, 0);
        }
        return b;
    }

    private static Buffer Settle(int rate)
    {
        var b = new Buffer(rate, 0.12);
        var rng = new Rng(9); var f = new Svf(rate);
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / rate;
            double v = Math.Sin(2 * Math.PI * 600 * t * (1 - 1.5 * t)) * Env(t, 0.003, 0.03) + f.Step(rng.Signed(), 1400, 0.9).band * Math.Exp(-t / 0.008) * 0.6;
            b.Add(i, v * 0.5, 0);
        }
        return b;
    }

    private static Buffer Sleep(int rate)
    {
        // a soft lullaby fall: C6 → A5 → F5 on felt piano + music box, with a sleepy breath "sigh"
        var b = new Buffer(rate, 2.0);
        double[] notes = { 84, 81, 77 };
        for (int i = 0; i < notes.Length; i++)
        {
            Instruments.Play(b, Instr.MusicBox, 0.02 + i * 0.13, notes[i], 0.4, 0.55 - i * 0.05, (i - 1) * 0.3, 41u + (uint)i);
            Instruments.Play(b, Instr.FeltPiano, 0.02 + i * 0.13, notes[i] - 12, 0.5, 0.35, 0, 51u + (uint)i);
        }
        var rng = new Rng(77); var f = new Svf(rate);
        for (int i = 0; i < (int)(0.55 * rate); i++)
        {
            double t = (double)i / rate, x = t / 0.55;
            b.Add(i + (int)(0.25 * rate), f.Step(rng.Signed(), 900 - 400 * x, 1.0).band * Math.Sin(Math.PI * x) * 0.12, 0);
        }
        return Looped.Tail(b, 0.6, 0.18);
    }

    private static Buffer IceSlide(int rate)
    {
        var b = new Buffer(rate, 0.45);
        var rng = new Rng(21); var f = new Svf(rate);
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / rate, x = t / 0.45;
            double c = 2200 + 1800 * x;
            double v = f.Step(rng.Signed(), c, 0.25).band * Math.Sin(Math.PI * Math.Min(1, x * 1.3)) * 0.5;
            v += 0.2 * Math.Sin(2 * Math.PI * (2600 + 600 * x) * t) * Math.Sin(Math.PI * x) * (0.5 + 0.5 * Math.Sin(2 * Math.PI * 23 * t));
            b.Add(i, v, 0);
        }
        return b;
    }

    private static Buffer IceStop(int rate)
    {
        var b = new Buffer(rate, 0.7);
        var rng = new Rng(22); var f = new Svf(rate);
        for (int i = 0; i < (int)(0.08 * rate); i++)
        {
            double t = (double)i / rate;
            b.Add(i, f.Step(rng.Signed(), 3000 - 15000 * t, 0.4).band * Math.Exp(-t / 0.03) * 0.5, 0);
        }
        Instruments.Play(b, Instr.Bells, 0.03, 100, 0.2, 0.22, 0, 5);
        return b;
    }

    private static Buffer Whoosh(int rate, double len, double from, double to, uint seed)
    {
        var b = new Buffer(rate, len);
        var rng = new Rng(seed); var fl = new Svf(rate); var fr = new Svf(rate);
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / rate, x = t / len;
            double c = from * Math.Pow(to / from, Math.Sin(Math.PI * x * 0.5));
            double env = Math.Pow(Math.Sin(Math.PI * Math.Pow(x, 0.7)), 1.5);
            b.L[i] += (float)(fl.Step(rng.Signed(), c, 0.35).band * env * 0.7);
            b.R[i] += (float)(fr.Step(rng.Signed(), c * 1.05, 0.35).band * env * 0.7);
        }
        return b;
    }

    private static Buffer Portal(int rate, bool rising)
    {
        var b = new Buffer(rate, 1.1);
        // swirling FM shimmer, arpeggiated up (enter) or down (exit), plus a whoosh
        double[] seq = rising ? new[] { 72.0, 76, 79, 84 } : new[] { 84.0, 79, 76, 72 };
        for (int i = 0; i < seq.Length; i++) Instruments.Play(b, Instr.Celesta, i * 0.05, seq[i], 0.2, 0.45, (i - 1.5) * 0.4, 31u + (uint)i);
        b.Mix(Whoosh(rate, 0.35, rising ? 800 : 2600, rising ? 2600 : 800, 6), 0.35);
        if (!rising) b.Mix(Plop(rate, 620, 0.12, 8), 0.7, (int)(0.18 * rate));
        return b;
    }

    private static Buffer PortalBlocked(int rate)
    {
        var b = new Buffer(rate, 0.35);
        for (int k = 0; k < 2; k++)
        {
            double f0 = k == 0 ? 520 : 440; int off = (int)(k * 0.12 * rate);
            for (int i = 0; i < (int)(0.12 * rate); i++)
            {
                double t = (double)i / rate;
                double v = Math.Sin(2 * Math.PI * f0 * t) + 0.35 * Math.Sin(2 * Math.PI * f0 * 2 * t) + 0.2 * Math.Sin(2 * Math.PI * f0 * 3.01 * t);
                b.Add(i + off, v * Env(t, 0.006, 0.05) * 0.35 * (1 + 0.3 * Math.Sin(2 * Math.PI * 30 * t)), 0);
            }
        }
        return b;
    }

    private static Buffer Sizzle(int rate)
    {
        var b = new Buffer(rate, 0.9);
        var rng = new Rng(33); var f = new Svf(rate);
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / rate, x = t / 0.9;
            double crack = rng.Uniform() < 0.004 ? rng.Signed() * 4 : 0;
            double v = f.Step(rng.Signed() * 0.6 + crack, 3600 - 1200 * x, 0.5).band * Env(t, 0.01, 0.25);
            b.Add(i, v * 0.6, 0);
        }
        b.Mix(LavaBubble(rate), 0.6);
        // a soft "uh-oh" fall so the failure reads without words
        for (int i = 0; i < (int)(0.35 * rate); i++)
        {
            double t = (double)i / rate;
            double fr = 660 * Math.Pow(0.75, t / 0.35);
            b.Add(i + (int)(0.1 * rate), (Math.Sin(2 * Math.PI * fr * t) + 0.3 * Math.Sin(4 * Math.PI * fr * t)) * Env(t, 0.01, 0.15) * 0.3, 0);
        }
        return b;
    }

    private static Buffer LavaBubble(int rate)
    {
        var b = new Buffer(rate, 0.35);
        double ph = 0;
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / rate;
            double f = 320 * (1 + 1.4 * (1 - Math.Exp(-t / 0.06)));
            ph += 2 * Math.PI * f / rate;
            b.Add(i, (Math.Sin(ph) + 0.55 * Math.Sin(2 * ph) + 0.3 * Math.Sin(3 * ph)) * Env(t, 0.005, 0.07) * 0.4, 0);
        }
        return b;
    }

    private static Buffer Jingle(int rate, double[] notes, double[] times, double len, bool perfect)
    {
        var b = new Buffer(rate, len + 0.5); // room for the tail before the closing fade
        for (int i = 0; i < notes.Length; i++)
        {
            bool last = i == notes.Length - 1;
            Instruments.Play(b, Instr.MusicBox, times[i], notes[i], 0.3, last ? 0.6 : 0.5, (i - notes.Length / 2.0) * 0.2, 60u + (uint)i);
            Instruments.Play(b, Instr.FeltPiano, times[i], notes[i] - 12, last ? 0.9 : 0.25, 0.45, 0, 70u + (uint)i);
        }
        double end = times[^1];
        // final F-major colour under the top note
        foreach (var m in new[] { 53.0, 60, 65 }) Instruments.Play(b, Instr.FeltPiano, end, m, 0.9, 0.32, 0, 90u + (uint)m);
        if (perfect)
        {
            for (int i = 0; i < 6; i++) Instruments.Play(b, Instr.Bells, end + 0.06 + i * 0.07, 89 + new[] { 0, 3, 7, 12, 7, 15 }[i], 0.2, 0.22, i % 2 == 0 ? -0.5 : 0.5, 80u + (uint)i);
        }
        return Looped.Tail(b, perfect ? 0.8 : 0.6, 0.22);
    }

    private static Buffer Deadlock(int rate)
    {
        var b = new Buffer(rate, 1.4);
        Instruments.Play(b, Instr.FeltPiano, 0.0, 81, 0.3, 0.5, 0, 1);
        Instruments.Play(b, Instr.FeltPiano, 0.22, 77, 0.6, 0.45, 0, 2);
        Instruments.Play(b, Instr.FeltPiano, 0.22, 70, 0.6, 0.3, 0, 3);
        Instruments.Play(b, Instr.MusicBox, 0.22, 89, 0.4, 0.18, 0.3, 4);
        Instruments.Play(b, Instr.Pad, 0.18, 74, 0.4, 0.4, 0, 5);
        return Looped.Tail(b, 0.5, 0.15);
    }

    private static Buffer Undo(int rate)
    {
        var b = new Buffer(rate, 0.22);
        double ph = 0;
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / rate, x = t / 0.22;
            double f = 900 * Math.Pow(0.6, x);
            ph += 2 * Math.PI * f / rate;
            b.Add(i, (Math.Sin(ph) + 0.25 * Math.Sin(2 * ph)) * Math.Sin(Math.PI * Math.Pow(x, 0.5)) * Math.Exp(-t / 0.12) * 0.5, 0);
        }
        return b;
    }

    private static Buffer Restart(int rate)
    {
        var b = new Buffer(rate, 0.5);
        double[] m = { 79, 76, 72 };
        for (int i = 0; i < 3; i++) b.Mix(Plop(rate, Hz(m[i]) * 0.8, 0.1, 100u + (uint)i), 0.7, (int)(i * 0.08 * rate));
        b.Mix(Whoosh(rate, 0.3, 2200, 700, 101), 0.25);
        return b;
    }

    private static Buffer Tap(int rate, double midi, double gain = 1)
    {
        var b = new Buffer(rate, 0.12);
        Instruments.Play(b, Instr.Marimba, 0, midi, 0.05, 0.5 * gain, 0, (uint)midi);
        // soften: fade the marimba tail so taps stay short
        for (int i = 0; i < b.Length; i++) { double t = (double)i / rate; double g = Math.Exp(-t / 0.035); b.L[i] *= (float)g; b.R[i] *= (float)g; }
        return b;
    }

    private static Buffer Dust(int rate)
    {
        var b = new Buffer(rate, 1.2);
        double[] m = { 89, 93, 96, 101 };
        for (int i = 0; i < m.Length; i++) Instruments.Play(b, Instr.Bells, i * 0.06, m[i], 0.2, 0.4, (i - 1.5) * 0.35, 200u + (uint)i);
        var rng = new Rng(202); var f = new Svf(rate);
        for (int i = 0; i < (int)(0.5 * rate); i++)
        {
            double t = (double)i / rate;
            double grain = rng.Uniform() < 0.02 ? rng.Signed() : 0;
            b.Add(i, f.Step(grain, 5000, 0.2).band * Math.Exp(-t / 0.2) * 0.6, rng.Signed() * 0.6);
        }
        return Looped.Tail(b, 0.6, 0.2);
    }

    private static Buffer CosmeticUnlock(int rate)
    {
        var b = new Buffer(rate, 2.0);
        double[] m = { 77, 81, 84, 89 };
        for (int i = 0; i < m.Length; i++)
        {
            Instruments.Play(b, Instr.Celesta, i * 0.08, m[i], 0.3, 0.45, (i - 1.5) * 0.3, 300u + (uint)i);
            Instruments.Play(b, Instr.MusicBox, i * 0.08, m[i] - 12, 0.3, 0.3, 0, 310u + (uint)i);
        }
        b.Mix(Dust(rate), 0.5, (int)(0.32 * rate));
        return Looped.Tail(b, 0.7, 0.22);
    }

    private static Buffer WorldUnlock(int rate)
    {
        // the motif's opening phrase, a swelling pad and bells: "a new place to explore"
        var b = new Buffer(rate, 2.6);
        double spb = 60.0 / 120;
        double[] mel = { 69, 72, 77, 76, 77 }; double[] dur = { 0.5, 0.5, 0.75, 0.25, 1.5 };
        double t = 0;
        for (int i = 0; i < mel.Length; i++)
        {
            Instruments.Play(b, Instr.Bells, t, mel[i], dur[i] * spb, 0.42, 0.1, 400u + (uint)i);
            Instruments.Play(b, Instr.FeltPiano, t, mel[i] - 12, dur[i] * spb, 0.4, -0.1, 410u + (uint)i);
            t += dur[i] * spb;
        }
        foreach (var m in new[] { 53.0, 60, 65, 69 }) Instruments.Play(b, Instr.Pad, 0, m, 1.6, 0.55, 0, 420u + (uint)m);
        Instruments.Play(b, Instr.Bass, 0, 41, 1.6, 0.5, 0, 430);
        b.FadeOut(0.4);
        return Looped.Tail(b, 0.8, 0.25);
    }

    /// <summary>
    /// The PLOP! sonic logo, ~1.1 s: a bubbly "plop" lands (the mascot hitting the ground on the splash) and the motif's
    /// first three notes ring out on music box + felt piano over an F-major chord, with a small bell sparkle.
    /// </summary>
    private static Buffer SonicLogo(int rate)
    {
        var b = new Buffer(rate, 1.15);
        b.Mix(Plop(rate, 520, 0.16, 500), 0.9);
        double[] m = { 69, 72, 77 }; double[] at = { 0.14, 0.26, 0.38 };
        for (int i = 0; i < m.Length; i++)
        {
            Instruments.Play(b, Instr.MusicBox, at[i], m[i], 0.3, 0.55, (i - 1) * 0.25, 510u + (uint)i);
            Instruments.Play(b, Instr.FeltPiano, at[i], m[i] - 12, i == 2 ? 0.6 : 0.2, 0.4, 0, 520u + (uint)i);
        }
        foreach (var n in new[] { 53.0, 57, 60 }) Instruments.Play(b, Instr.FeltPiano, 0.38, n, 0.6, 0.28, 0, 530u + (uint)n);
        Instruments.Play(b, Instr.Bells, 0.46, 89, 0.3, 0.25, 0.45, 540);
        Instruments.Play(b, Instr.Bells, 0.52, 93, 0.3, 0.2, -0.45, 541);
        var outBuf = Looped.Tail(b, 0.7, 0.2);
        outBuf.FadeOut(0.25);
        return outBuf;
    }
}

