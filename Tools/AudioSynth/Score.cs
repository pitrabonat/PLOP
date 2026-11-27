// PLOP! offline audio synthesis — the original PLOP! motif and its arrangements (menu + nine Worlds).
// Composition: original, written for PLOP! in this repository. 16 bars, 4/4, F major.
namespace PlopAudioSynth;

public readonly record struct NoteEvent(Instr Instr, double Beat, double Midi, double Beats, double Vel, double Pan);

public sealed record Arrangement(
    string Id, string Title, double Bpm, int Transpose,
    Instr Lead, int LeadOctave, double LeadVel,
    Instr? Arp, int ArpOctave, string ArpStyle,
    Instr? Pad, Instr? Bass, Instr? Sparkle, Instr? Counter,
    double ReverbWet, double Room, string Mood);

public static class Score
{
    public const int Bars = 16;
    public const int BeatsPerBar = 4;
    public const double LoopBeats = Bars * BeatsPerBar;

    // Chords, one per bar (bar 15 splits Gm | C). Root in MIDI octave 3; triad intervals.
    private static readonly (int root, int[] triad)[] Chords =
    {
        (53, new[] { 0, 4, 7 }),  // F
        (57, new[] { 0, 3, 7 }),  // Am
        (58, new[] { 0, 4, 7 }),  // Bb
        (48, new[] { 0, 4, 7 }),  // C
        (50, new[] { 0, 3, 7 }),  // Dm
        (58, new[] { 0, 4, 7 }),  // Bb
        (55, new[] { 0, 3, 7 }),  // Gm
        (48, new[] { 0, 4, 7 }),  // C
        (53, new[] { 0, 4, 7 }),  // F
        (57, new[] { 0, 3, 7 }),  // Am
        (58, new[] { 0, 4, 7 }),  // Bb
        (53, new[] { 0, 4, 7 }),  // F
        (50, new[] { 0, 3, 7 }),  // Dm
        (58, new[] { 0, 4, 7 }),  // Bb
        (55, new[] { 0, 3, 7 }),  // Gm (beats 1-2) then C (3-4)
        (53, new[] { 0, 4, 7 }),  // F
    };

    // The PLOP! motif (MIDI, beats). Rests are midi 0. Octave 4/5 as written.
    private static readonly (int bar, double[] notes)[] Melody =
    {
        (0,  new double[] { 69, 1, 72, 1, 77, 1.5, 76, 0.5 }),
        (1,  new double[] { 76, 1, 72, 1, 69, 2 }),
        (2,  new double[] { 74, 1, 77, 1, 74, 1, 70, 1 }),
        (3,  new double[] { 72, 3, 0, 1 }),
        (4,  new double[] { 69, 1, 74, 1, 77, 1.5, 76, 0.5 }),
        (5,  new double[] { 74, 1, 70, 1, 77, 2 }),
        (6,  new double[] { 79, 1, 77, 1, 76, 1, 74, 1 }),
        (7,  new double[] { 76, 3, 0, 1 }),
        (8,  new double[] { 69, 1, 72, 1, 77, 1.5, 79, 0.5 }),
        (9,  new double[] { 81, 1, 79, 1, 76, 2 }),
        (10, new double[] { 77, 1, 74, 1, 70, 1, 74, 1 }),
        (11, new double[] { 72, 2, 69, 1, 72, 1 }),
        (12, new double[] { 74, 1, 77, 1, 81, 1.5, 79, 0.5 }),
        (13, new double[] { 77, 1, 74, 1, 70, 2 }),
        (14, new double[] { 67, 1, 70, 1, 74, 1, 76, 1 }),
        (15, new double[] { 77, 3, 0, 1 }),
    };

    // A gentle counter-line (second half only), answering the motif.
    private static readonly (int bar, double[] notes)[] CounterLine =
    {
        (8,  new double[] { 0, 2, 72, 2 }),
        (9,  new double[] { 72, 2, 71, 2 }),
        (10, new double[] { 70, 4 }),
        (11, new double[] { 69, 2, 65, 2 }),
        (12, new double[] { 65, 2, 69, 2 }),
        (13, new double[] { 70, 4 }),
        (14, new double[] { 70, 2, 72, 2 }),
        (15, new double[] { 69, 4 }),
    };

    public static readonly Arrangement Menu = new("menu", "PLOP! Main Theme", 84, 0,
        Instr.FeltPiano, 0, 0.62, Instr.MusicBox, 1, "broken", Instr.Pad, Instr.Bass, Instr.Bells, Instr.Flute, 0.32, 0.84,
        "felt piano lead, music-box arpeggio, warm pad, round bass, bell sparkles, flute answer in the second half");

    public static readonly Arrangement[] Worlds =
    {
        new("cozy-nest", "Cozy Nest", 80, 0, Instr.FeltPiano, 0, 0.55, Instr.Kalimba, 0, "broken", Instr.Pad, Instr.Bass, null, null, 0.28, 0.80,
            "felt piano and kalimba by a warm fire; the simplest telling of the motif"),
        new("dewdrop-garden", "Dewdrop Garden", 88, 0, Instr.Marimba, 0, 0.62, Instr.Pluck, 0, "bounce", null, Instr.Bass, Instr.MusicBox, Instr.Flute, 0.24, 0.78,
            "bright marimba, plucked bounce, flute answer; morning-garden energy"),
        new("rainy-window", "Rainy Window", 76, -2, Instr.FeltPiano, 0, 0.5, Instr.FeltPiano, -1, "slow", Instr.Pad, null, Instr.Celesta, null, 0.40, 0.88,
            "slow felt piano in E-flat, sparse left hand, soft pad; reflective"),
        new("twilight-attic", "Twilight Attic", 78, -3, Instr.MusicBox, 0, 0.58, Instr.FeltPiano, -1, "broken", Instr.Pad, Instr.Bass, null, null, 0.34, 0.84,
            "music-box melody over low piano in D; a dusty lullaby"),
        new("moonlit-clouds", "Moonlit Clouds", 74, 2, Instr.Celesta, 0, 0.55, Instr.Bells, 0, "slow", Instr.Pad, null, null, Instr.Flute, 0.46, 0.90,
            "celesta and slow bells in G, floating pad, airy flute"),
        new("frosted-pond", "Frosted Pond", 82, 2, Instr.Bells, 0, 0.5, Instr.Celesta, 0, "broken", Instr.Pad, Instr.Bass, Instr.MusicBox, null, 0.42, 0.88,
            "glassy bells and celesta in G; crisp and cold but still warm"),
        new("breezy-meadow", "Breezy Meadow", 90, 2, Instr.Flute, 0, 0.6, Instr.Marimba, 0, "bounce", null, Instr.Bass, Instr.Kalimba, Instr.Pluck, 0.26, 0.78,
            "flute lead over marimba bounce in G; the liveliest arrangement"),
        new("ember-hollow", "Ember Hollow", 80, -5, Instr.Kalimba, 0, 0.62, Instr.Marimba, -1, "broken", Instr.Pad, Instr.Bass, null, null, 0.30, 0.82,
            "kalimba over low marimba in C; warm, earthy, glowing"),
        new("prism-observatory", "Prism Observatory", 86, 3, Instr.Celesta, 0, 0.55, Instr.Bells, 1, "broken", Instr.Pad, Instr.Bass, Instr.MusicBox, Instr.Flute, 0.40, 0.88,
            "celesta, prismatic bells and pad in A-flat; wonder"),
    };

    public static List<NoteEvent> Build(Arrangement a, uint seed)
    {
        var ev = new List<NoteEvent>();
        var rng = new Rng(seed);
        int tr = a.Transpose;

        // Lead: the motif. "slow" arrangements leave the last eighth-notes as quarter-note sustains.
        foreach (var (bar, notes) in Melody)
        {
            double beat = bar * BeatsPerBar;
            for (int i = 0; i < notes.Length; i += 2)
            {
                double midi = notes[i], len = notes[i + 1];
                if (midi > 0)
                {
                    double accent = (beat % BeatsPerBar == 0 ? 1.0 : 0.88) * (bar % 4 == 3 ? 0.9 : 1);
                    ev.Add(new NoteEvent(a.Lead, beat + Humanize(rng), midi + tr + 12 * a.LeadOctave,
                        len * 0.95, a.LeadVel * accent * Range(rng, 0.92, 1.0), Range(rng, -0.08, 0.08)));
                }
                beat += len;
            }
        }

        if (a.Counter is Instr counter)
            foreach (var (bar, notes) in CounterLine)
            {
                double beat = bar * BeatsPerBar;
                for (int i = 0; i < notes.Length; i += 2)
                {
                    if (notes[i] > 0)
                        ev.Add(new NoteEvent(counter, beat + Humanize(rng), notes[i] + tr - (counter == Instr.Flute ? 0 : 12),
                            notes[i + 1] * 0.9, 0.34 * Range(rng, 0.9, 1), 0.45));
                    beat += notes[i + 1];
                }
            }

        for (int bar = 0; bar < Bars; bar++)
        {
            double b0 = bar * BeatsPerBar;
            var halves = bar == 14
                ? new[] { (Chords[14], 0.0, 2.0), ((48, new[] { 0, 4, 7 }), 2.0, 2.0) }
                : new[] { (Chords[bar], 0.0, 4.0) };

            foreach (var ((root, triad), off, len) in halves)
            {
                int r = root + tr;
                // keep accompaniment in a comfortable register
                while (r < 50) r += 12;
                while (r > 61) r -= 12;

                if (a.Pad is Instr pad)
                    foreach (int iv in triad)
                        ev.Add(new NoteEvent(pad, b0 + off, r + iv + 12, len, 0.42, iv == 4 || iv == 3 ? -0.35 : 0.35));

                if (a.Bass is Instr bass)
                {
                    double bm = r - 12;
                    ev.Add(new NoteEvent(bass, b0 + off, bm, len >= 4 ? 1.8 : 1.6, 0.62, 0));
                    if (len >= 4) ev.Add(new NoteEvent(bass, b0 + off + 2, bm + (bar % 2 == 0 ? 7 : 0), 1.6, 0.5, 0));
                }

                if (a.Arp is Instr arp)
                {
                    int ao = 12 * a.ArpOctave;
                    int[] tones = { r + triad[0] + ao, r + triad[1] + ao, r + triad[2] + ao, r + 12 + ao };
                    double[] pattern = a.ArpStyle switch
                    {
                        "slow" => new double[] { 0, 1, 2, 3 },              // quarter notes
                        "bounce" => new double[] { 0, 0.5, 1.5, 2, 2.5, 3.5 }, // syncopated
                        _ => new double[] { 0, 0.5, 1, 1.5, 2, 2.5, 3, 3.5 },  // eighths
                    };
                    int[] order = { 0, 2, 1, 3, 2, 1, 3, 2 };
                    for (int i = 0; i < pattern.Length; i++)
                    {
                        if (pattern[i] >= len) continue;
                        int tone = tones[order[i % order.Length]];
                        double vel = (pattern[i] % 1 == 0 ? 0.36 : 0.28) * Range(rng, 0.85, 1);
                        ev.Add(new NoteEvent(arp, b0 + off + pattern[i] + Humanize(rng) * 0.6, tone,
                            a.ArpStyle == "slow" ? 0.95 : 0.45, vel, i % 2 == 0 ? -0.4 : 0.4));
                    }
                }
            }

            // Sparkles: a high answer at the end of every second bar.
            if (a.Sparkle is Instr sp && bar % 2 == 1)
            {
                var (root, triad) = Chords[bar];
                int top = root + tr + triad[2];
                while (top < 84) top += 12;
                ev.Add(new NoteEvent(sp, b0 + 3 + Humanize(rng), top, 0.5, 0.22, 0.6));
                ev.Add(new NoteEvent(sp, b0 + 3.5 + Humanize(rng), top - 3, 0.5, 0.18, -0.6));
            }
        }
        return ev;
    }

    private static double Humanize(Rng rng) => rng.Signed() * 0.012; // ±12 ms-ish at these tempi
    private static double Range(Rng rng, double a, double b) => rng.Range(a, b);

    /// <summary>
    /// Renders a seamless loop: every event is rendered at beat b and b + loop, reverb runs over both passes, and
    /// the second pass is kept, so tails that cross the loop end are already present at the loop start.
    /// </summary>
    public static Buffer RenderLoop(Arrangement a, int rate, uint seed)
    {
        double spb = 60.0 / a.Bpm;
        double loopSec = LoopBeats * spb;
        int loopFrames = (int)Math.Round(loopSec * rate);
        var events = Build(a, seed);
        var buffer = new Buffer(rate, 2 * loopFrames);
        foreach (var e in events)
            for (int pass = 0; pass < 2; pass++)
            {
                double t = (e.Beat + pass * LoopBeats) * spb;
                if (t < 0) t += 2 * loopSec;
                Instruments.Play(buffer, e.Instr, t, e.Midi, e.Beats * spb, e.Vel, e.Pan,
                    seed + (uint)(e.Beat * 64) + (uint)(e.Midi * 3)); // same seed in both passes: wrapped tails match
            }
        new Reverb(rate, a.Room, 0.38).Process(buffer, a.ReverbWet, 1);
        return buffer.Slice(loopFrames, loopFrames);
    }

    public static double LoopSeconds(Arrangement a) => LoopBeats * 60.0 / a.Bpm;
}
