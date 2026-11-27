// PLOP! AudioSynth — renders every music loop, ambience bed and effect for the game from code.
//   dotnet run -c Release --project Tools/AudioSynth -- [--runtime <Assets/Audio/Resources>] [--masters <dir>] [--only <id>]
// Runtime files: 16-bit WAV (music 32 kHz stereo, ambience 24 kHz stereo, effects 32 kHz mono); Unity transcodes them.
// Masters: 24-bit 44.1 kHz WAV for marketing/trailer use, plus manifest.json / MANIFEST.md / levels report.
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using PlopAudioSynth;
using Buffer = PlopAudioSynth.Buffer;

string repo = FindRepo();
string runtime = Arg("--runtime") ?? Path.Combine(repo, "Assets", "Audio", "Resources");
string masters = Arg("--masters") ?? "/Volumes/PLOP_DEV/Marketing/Audio";
string only = Arg("--only");
const uint Seed = 20261127;
const string Version = "S02-audio-1";

var jobs = new List<(string kind, string id, string title, string use, Func<int, Buffer> render, Action<Buffer> master, int runtimeRate, bool runtimeMono, bool loop)>();

foreach (var a in new[] { Score.Menu }.Concat(Score.Worlds))
    jobs.Add(("Music", a.Id, a.Title, a.Id == "menu" ? "menu, Studio, World select and Settings (continuous)" : $"gameplay music, World '{a.Title}': {a.Mood}",
        r => Score.RenderLoop(a, r, Seed + (uint)a.Id.GetHashCode32()), b => b.Master(-20, -1), 32000, false, true));
foreach (var bed in Ambience.Beds)
    jobs.Add(("Ambience", bed.Id, bed.Id, "World ambience bed: " + bed.Description,
        r => Ambience.Render(bed.Id, r, Seed ^ (uint)bed.Id.GetHashCode32()), b => b.Master(-26, -6), 24000, false, true));
foreach (var cue in Sfx.All())
    jobs.Add(("Sfx", cue.Id, cue.Id, cue.Use, cue.Render, b => { b.FadeIn(0.002); b.FadeOut(Math.Min(0.45, 0.3 * b.Length / (double)b.Rate)); b.NormalizePeak(cue.PeakDb); b.SoftLimit(Math.Pow(10, -1 / 20.0)); }, 32000, true, false));

if (only != null) jobs = jobs.Where(j => j.id == only || j.kind == only).ToList();

var rows = new ConcurrentBag<Dictionary<string, object>>();
var sw = System.Diagnostics.Stopwatch.StartNew();
Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount - 2) }, j =>
{
    var rt = j.render(j.runtimeRate); j.master(rt);
    string rtPath = Path.Combine(runtime, j.kind, j.id + ".wav");
    rt.WriteWav(rtPath, j.runtimeMono, 16);

    var m = j.render(44100); j.master(m);
    string mPath = Path.Combine(masters, j.kind == "Sfx" ? "SFX" : j.kind, $"PLOP_{j.kind}_{j.id}.wav");
    m.WriteWav(mPath, j.kind == "Sfx", 24);

    rows.Add(new Dictionary<string, object>
    {
        ["kind"] = j.kind, ["id"] = j.id, ["title"] = j.title, ["use"] = j.use,
        ["master"] = Path.GetFileName(mPath), ["runtime"] = $"Assets/Audio/Resources/{j.kind}/{j.id}.wav",
        ["seconds"] = Math.Round((double)m.Length / m.Rate, 3), ["loop"] = j.loop,
        ["peak_dbfs"] = Db(m.Peak()), ["rms_dbfs"] = Db(m.Rms()),
        ["runtime_peak_dbfs"] = Db(rt.Peak()), ["runtime_rms_dbfs"] = Db(rt.Rms()),
        ["speaker_band_share"] = Math.Round(BandShare(m, 400, 4000), 3),
        ["loop_seam_ratio"] = j.loop ? Math.Round(SeamRatio(rt), 2) : 0,
        ["source"] = "PLOP! AudioSynth (Tools/AudioSynth), procedural synthesis, seed " + Seed,
        ["license"] = "Original work created for PLOP!; no samples, loops or third-party recordings",
        ["runtime_version"] = Version,
    });
    Console.WriteLine($"{j.kind,-9} {j.id,-20} {Db(m.Peak()),6:0.0} pk {Db(m.Rms()),6:0.0} rms  band {BandShare(m, 400, 4000):0.00}");
});

var sorted = rows.OrderBy(r => (string)r["kind"]).ThenBy(r => (string)r["id"]).ToList();
if (only == null)
{
    Directory.CreateDirectory(masters);
    File.WriteAllText(Path.Combine(masters, "manifest.json"), JsonSerializer.Serialize(sorted, new JsonSerializerOptions { WriteIndented = true }));
    var md = new StringBuilder();
    md.AppendLine("# PLOP! audio manifest").AppendLine();
    md.AppendLine($"Rendered by `Tools/AudioSynth` (runtime version `{Version}`, seed {Seed}). Every sound is synthesized from code in this");
    md.AppendLine("repository: no samples, sample packs, loops or third-party recordings, and the music is an original composition.").AppendLine();
    md.AppendLine("Masters: 24-bit 44.1 kHz WAV (effects mono, music and ambience stereo). Runtime copies in `Assets/Audio/Resources`.").AppendLine();
    md.AppendLine("| File | Seconds | Loop | Peak dBFS | RMS dBFS | Use | Source | License | Runtime |");
    md.AppendLine("|---|---:|:-:|---:|---:|---|---|---|---|");
    foreach (var r in sorted)
        md.AppendLine($"| {r["master"]} | {r["seconds"]} | {((bool)r["loop"] ? "yes" : "")} | {r["peak_dbfs"]} | {r["rms_dbfs"]} | {r["use"]} | AudioSynth | Original (PLOP!) | `{r["runtime"]}` ({Version}) |");
    File.WriteAllText(Path.Combine(masters, "MANIFEST.md"), md.ToString());
    File.WriteAllText(Path.Combine(repo, "Tools", "AudioSynth", "levels.json"), JsonSerializer.Serialize(sorted, new JsonSerializerOptions { WriteIndented = true }));
}
Console.WriteLine($"{jobs.Count} assets in {sw.Elapsed.TotalSeconds:0.0} s");

static double Db(double x) => Math.Round(20 * Math.Log10(Math.Max(x, 1e-9)), 1);

// Share of energy between lo and hi Hz (two cascaded SVF band edges) — a proxy for "audible on a phone speaker".
static double BandShare(Buffer b, double lo, double hi)
{
    var hp1 = new Svf(b.Rate); var hp2 = new Svf(b.Rate); var lp1 = new Svf(b.Rate); var lp2 = new Svf(b.Rate);
    double total = 0, band = 0;
    for (int i = 0; i < b.Length; i++)
    {
        double x = 0.5 * (b.L[i] + b.R[i]);
        double h = hp2.Step(hp1.Step(x, lo, 1.4).high, lo, 1.4).high;
        double y = lp2.Step(lp1.Step(h, hi, 1.4).low, hi, 1.4).low;
        total += x * x; band += y * y;
    }
    return total > 0 ? band / total : 0;
}

// |last→first sample jump| relative to the median sample-to-sample step: ≈1 means the seam is invisible.
static double SeamRatio(Buffer b)
{
    int n = b.Length;
    var steps = new double[Math.Min(n - 1, 200000)];
    for (int i = 0; i < steps.Length; i++) steps[i] = Math.Abs(b.L[i + 1] - b.L[i]);
    Array.Sort(steps);
    double median = Math.Max(1e-6, steps[steps.Length / 2]);
    return Math.Abs(b.L[0] - b.L[n - 1]) / median;
}

string Arg(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }

static string FindRepo()
{
    var d = new DirectoryInfo(AppContext.BaseDirectory);
    while (d != null && !Directory.Exists(Path.Combine(d.FullName, "Assets"))) d = d.Parent;
    return d?.FullName ?? Directory.GetCurrentDirectory();
}

static class Hash
{
    // Stable across runs (string.GetHashCode is randomized per process).
    public static int GetHashCode32(this string s) { unchecked { int h = 23; foreach (char c in s) h = h * 31 + c; return h; } }
}
