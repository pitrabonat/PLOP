using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Plop.Core;
using Plop.Solver;

namespace Plop.Generation
{
    /// <summary>Where a Phase 11 campaign level came from.</summary>
    public enum LevelProvenance
    {
        /// <summary>The Phase 10 level unchanged (same id: records and Perfects carry over).</summary>
        Kept,
        /// <summary>The Phase 10 board with decorative or idle tiles removed (new id).</summary>
        Improved,
        /// <summary>A new searched level in place of a weak one (new id).</summary>
        Replaced,
        /// <summary>A hand-authored board (Phase 11 onboarding).</summary>
        Authored
    }

    /// <summary>Learning structure of a mechanic (Phase 11 brief §10) or the role of a later level.</summary>
    public enum LearningStage { Basics, Discovery, Application, Reasoning, Combination, Mastery, Breather, Mixed }

    public sealed class BalancedLevel
    {
        public int Index;
        public string WorldId;
        public LevelProvenance Provenance;
        public LearningStage Stage;
        public LevelDefinition Level;
        /// <summary>One-based Phase 10 level this slot keeps, improves or replaces (−1 = new content).</summary>
        public int PreviousLevel = -1;
        public string PreviousId;
        /// <summary>Why the Phase 10 level was not kept as is.</summary>
        public string Weakness = "";
        /// <summary>The reasoning challenge the slot is meant to pose.</summary>
        public string Intent = "";
        public PuzzleQualityReport Quality;
        public PuzzleInsight Insight;
        public int MoveLimitSlack = -1;
        /// <summary>The board is unchanged but its Limited Moves setting changed, so its id (and record) is new.</summary>
        public bool LimitChanged;
    }

    public sealed class BalancedBuild
    {
        public bool Success;
        public string Message = "";
        public List<BalancedLevel> Levels = new List<BalancedLevel>();
        public List<QualityAuditRow> Previous = new List<QualityAuditRow>();
        public int Searches;
        public double Seconds;
    }

    /// <summary>
    /// Phase 11 campaign rebalance. Starts from the shipped Phase 10 pack and its <see cref="QualityAudit"/>:
    /// strong levels are kept (same id), weak ones are improved (decorative tiles removed) or replaced by
    /// searched levels that pass <see cref="QualityFloors"/>; the opening is re-authored so a mechanic arrives at
    /// level 6; each mechanic World follows Discovery → Application → Reasoning → Combination → Mastery.
    /// Deterministic for a given input pack and seed. Offline only.
    /// </summary>
    public static class BalancedCampaign
    {
        public const int Version = 2;
        /// <summary>Search seeds stay those of v1 so a rule change only re-searches the slots it affects.</summary>
        public const int SeedVersion = 1;
        public const int LevelCount = 160;
        public const string PackId = "PLOP_Pack_003";
        public const ulong DefaultSeed = 0x504C4F50_31310000UL;
        public const int MaxRetries = 48;
        public const int CandidatesPerSlot = 4;

        public static readonly (string Id, int First, int Last)[] Worlds =
        {
            ("cozy-nest", 1, 5),
            ("frosted-pond", 6, 20),
            ("breezy-meadow", 21, 34),
            ("ember-hollow", 35, 48),
            ("prism-observatory", 49, 63),
            ("dewdrop-garden", 64, 85),
            ("rainy-window", 86, 108),
            ("twilight-attic", 109, 133),
            ("moonlit-clouds", 134, 160),
        };

        /// <summary>Limited Moves challenges: one-based level → moves allowed above the optimum.</summary>
        public static readonly (int Level, int Slack)[] Challenges =
        {
            (30, 2), (45, 2), (60, 2), (80, 1), (100, 1), (120, 1), (140, 0), (155, 0)
        };

        public static string WorldOf(int level)
        {
            foreach (var w in Worlds) if (level >= w.First && level <= w.Last) return w.Id;
            return Worlds[Worlds.Length - 1].Id;
        }

        public static int ChallengeSlack(int level)
        {
            foreach (var c in Challenges) if (c.Level == level) return c.Slack;
            return -1;
        }

        // ------------------------------------------------------------------ authored onboarding (levels 2, 4, 6–10)

        private sealed class Authored
        {
            public int Level;
            public string[] Rows;
            public string Instruction;
            public string Intent;
            public LearningStage Stage;
        }

        private static readonly Authored[] Onboarding =
        {
            new Authored { Level = 2, Stage = LearningStage.Basics, Rows = new[] { "R#.", "..r", "..." },
                Intent = "Walls block: the straight swipe bumps, so go around." },
            new Authored { Level = 4, Stage = LearningStage.Basics, Rows = new[] { "b...", "r#..", "..R.", "#B.." },
                Intent = "Use walls to hold one Plop while the other moves." },
            new Authored { Level = 6, Stage = LearningStage.Discovery, Rows = new[] { ".#r#", "R.~.", ".~..", "...." },
                Instruction = "Frost: Plops slide until something stops them.",
                Intent = "See a slide; find the swipe that ends under the Nest." },
            new Authored { Level = 7, Stage = LearningStage.Application, Rows = new[] { "r...#", "##.#.", ".R~..", ".~...", "....." },
                Intent = "Predict where the slide stops; approach from the side that stops in the right place." },
            new Authored { Level = 8, Stage = LearningStage.Reasoning, Rows = new[] { "..#B", ".#.~", "brR.", "#.~.", "...." },
                Intent = "Frost with a second Plop: a sleeping Plop becomes a stopper." },
            new Authored { Level = 9, Stage = LearningStage.Reasoning, Rows = new[] { "..br", "#..#", "...B", "..#R", "...." },
                Intent = "Coordination: settle in the right order so neither Plop blocks the other." },
            new Authored { Level = 10, Stage = LearningStage.Mastery, Rows = new[] { "r.#..", "B.b..", ".##..", "#..~.", ".R..." },
                Intent = "First mastery puzzle: plan the slide and the settle order together." },
        };

        /// <summary>Phase 10 levels kept verbatim in the opening (one-based Phase 10 number → Phase 11 number).</summary>
        private static readonly (int Old, int New, LearningStage Stage, string Intent)[] KeptOpening =
        {
            (1, 1, LearningStage.Basics, "Swipe into the matching Nest."),
            (3, 3, LearningStage.Basics, "One swipe moves every awake Plop."),
            (5, 5, LearningStage.Basics, "First real coordination: the order of settling matters."),
            (35, 35, LearningStage.Discovery, "Lava is visible danger: walk around it."),
        };

        private static readonly Dictionary<string, (MechanicSet Main, MechanicSet Second, MechanicSet Third)> MechanicWorlds =
            new Dictionary<string, (MechanicSet, MechanicSet, MechanicSet)>
            {
                ["frosted-pond"] = (MechanicSet.Frost, MechanicSet.None, MechanicSet.None),
                ["breezy-meadow"] = (MechanicSet.Breeze, MechanicSet.Frost, MechanicSet.None),
                ["ember-hollow"] = (MechanicSet.Lava, MechanicSet.Frost, MechanicSet.Breeze),
                ["prism-observatory"] = (MechanicSet.Loops, MechanicSet.Lava, MechanicSet.Frost),
            };

        private static readonly Dictionary<string, string> DiscoveryText = new Dictionary<string, string>
        {
            ["breezy-meadow"] = "Breeze fans push Plops the way they point. Dots show how far.",
            ["prism-observatory"] = "Loops connect: in one, out the other.",
        };

        // ------------------------------------------------------------------ weakness and floors

        /// <summary>Why an audited Phase 10 level should not ship unchanged in Phase 11 (empty = keep).</summary>
        public static List<string> Weakness(QualityAuditRow row) => Weakness(row, false);

        /// <summary><paramref name="anyPosition"/>: apply the position-dependent rules everywhere (Daily Puzzles have no early slots).</summary>
        public static List<string> Weakness(QualityAuditRow row, bool anyPosition)
        {
            var reasons = new List<string>();
            if (!row.Verified || !row.Analysed) { reasons.Add("unverified"); return reasons; }
            foreach (var f in row.Flags)
            {
                if (f == "random-solvable" || f == "greedy-solvable" || f == "few-decisions" || f == "long-for-its-decisions" || f == "decorative-mechanic")
                    reasons.Add(f);
                if (f == "shallow" && (anyPosition || row.Index >= 10)) reasons.Add(f);
            }
            if (row.Quality.LongestRun >= 4 && row.Quality.Quality < 70) reasons.Add("repetitive run " + row.Quality.LongestRun);
            if ((anyPosition || row.Index >= 10) && row.Quality.Quality < 40) reasons.Add("low quality " + row.Quality.Quality.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
            int present = 0, matters = 0;
            foreach (var m in row.Quality.Mechanics)
            {
                if (m.Role == MechanicRole.Absent) continue;
                present++;
                if (m.Role == MechanicRole.Essential || m.Role == MechanicRole.Hazard || m.Role == MechanicRole.Unknown) matters++;
            }
            if (present > 0 && matters == 0) reasons.Add("no mechanic changes the solution");
            return reasons;
        }

        public static QualityFloors FloorsFor(LearningStage stage)
        {
            switch (stage)
            {
                case LearningStage.Discovery: return new QualityFloors { MaxRandomSuccess = 0.6, MinDecisions = 1, MaxLongestRun = 3, MaxIdleTiles = 1 };
                case LearningStage.Application: return new QualityFloors { MaxRandomSuccess = 0.3, MinDecisionDensity = 0.25, MinDecisions = 2, MaxLongestRun = 3, MaxIdleTiles = 1 };
                case LearningStage.Reasoning: return new QualityFloors { MinPlanningDepth = 3, MaxRandomSuccess = 0.12, MinDecisionDensity = 0.25, MaxLongestRun = 3, MaxIdleTiles = 1, MinQuality = 45 };
                case LearningStage.Combination: return new QualityFloors { MinPlanningDepth = 3, MaxRandomSuccess = 0.08, MinDecisionDensity = 0.25, MaxLongestRun = 3, MaxIdleTiles = 1, MinQuality = 50 };
                case LearningStage.Mastery: return new QualityFloors { MinPlanningDepth = 4, MaxRandomSuccess = 0.03, MinDecisionDensity = 0.3, MaxLongestRun = 3, MaxIdleTiles = 1, MinQuality = 65 };
                case LearningStage.Breather: return new QualityFloors { MinPlanningDepth = 2, MaxRandomSuccess = 0.2, MinDecisionDensity = 0.25, MaxLongestRun = 3, MaxIdleTiles = 1, MinQuality = 40 };
                default: return new QualityFloors { MinPlanningDepth = 3, MaxRandomSuccess = 0.05, MinDecisionDensity = 0.3, MaxLongestRun = 3, MaxIdleTiles = 1, MinQuality = 55 };
            }
        }

        public static QualityTarget StageTarget(string worldId, LearningStage stage)
        {
            var m = MechanicWorlds[worldId];
            var t = new QualityTarget { Restarts = 30, StepsPerRestart = 180, MaxUnusedSpace = 0.55, MaxRepetition = 0.4, MaxFans = 2, MaxTiles = 4 };
            switch (stage)
            {
                case LearningStage.Discovery:
                    t.Width = 4; t.Height = 4; t.Plops = 1; t.MinOptimal = 2; t.MaxOptimal = 5; t.MaxTiles = 2; t.MaxWalls = 3;
                    t.Mechanics = t.RequiredUse = m.Main;
                    break;
                case LearningStage.Application:
                    t.Width = 5; t.Height = 5; t.Plops = 1; t.MinOptimal = 4; t.MaxOptimal = 8; t.MaxTiles = 3; t.MaxWalls = 6;
                    t.Mechanics = t.RequiredUse = m.Main; t.MinDecisionPoints = 2; t.MaxRandomSolve = 0.3; t.RequireGreedyFail = true;
                    // One Plop rarely has a real choice around a single Loop pair or a Lava pool.
                    if (m.Main == MechanicSet.Loops || m.Main == MechanicSet.Lava) t.Plops = 2;
                    break;
                case LearningStage.Reasoning:
                    t.Width = 5; t.Height = 5; t.Plops = 2; t.MinOptimal = 6; t.MaxOptimal = 10; t.MaxTiles = 3; t.MaxWalls = 6;
                    t.Mechanics = t.RequiredUse = m.Main; t.RequireGreedyFail = true;
                    break;
                case LearningStage.Combination:
                    t.Width = 5; t.Height = 6; t.Plops = 2; t.MinOptimal = 6; t.MaxOptimal = 12; t.MaxTiles = 4; t.MaxWalls = 8;
                    t.Mechanics = m.Main | m.Second; t.RequiredUse = m.Second == MechanicSet.None ? m.Main : m.Main | m.Second;
                    t.RequireGreedyFail = true; t.MinStrategicBlocks = m.Second == MechanicSet.None ? 1 : 0;
                    break;
                default: // Mastery
                    t.Width = 6; t.Height = 6; t.Plops = 2; t.MinOptimal = 8; t.MaxOptimal = 13; t.MaxTiles = 5; t.MaxWalls = 10;
                    t.Mechanics = m.Main | m.Second | m.Third; t.RequiredUse = m.Main; t.RequireGreedyFail = true; t.MinTraps = 1;
                    t.SolverMaxStates = 400_000;
                    break;
            }
            t.MinMechanicSteps = (t.RequiredUse & ~MechanicSet.Lava) != MechanicSet.None ? 1 : 0;
            return t;
        }

        private static string IntentFor(string worldId, LearningStage stage)
        {
            var m = MechanicWorlds.TryGetValue(worldId, out var w) ? w.Main.ToString() : "the mechanics";
            switch (stage)
            {
                case LearningStage.Discovery: return "Discover " + m + " on a tiny board.";
                case LearningStage.Application: return "Use " + m + " on purpose to reach the Nest.";
                case LearningStage.Reasoning: return "Predict " + m + " outcomes for two Plops at once.";
                case LearningStage.Combination: return "Combine " + m + " with " + (w.Second == MechanicSet.None ? "sleeping-Plop stoppers" : w.Second.ToString()) + ".";
                case LearningStage.Mastery: return "Multi-step " + m + " plan with a tempting trap.";
                case LearningStage.Breather: return "A lighter puzzle between peaks.";
                default: return "Combined-mechanic reasoning at this World's difficulty.";
            }
        }

        // ------------------------------------------------------------------ build

        private sealed class Job
        {
            public int Slot = -1;          // fixed position (combination Worlds) or −1 (mechanic Worlds: ordered later)
            public string WorldId;
            public LearningStage Stage;
            public QualityTarget Target;
            public QualityFloors Floors;
            public int Previous = -1;      // zero-based Phase 10 index this job replaces
            public string Weakness = "";
            public ulong SeedIndex;
            public List<(LevelDefinition Level, StateSpaceAnalysis Analysis, PuzzleInsight Insight, PuzzleQualityReport Quality, ulong Seed, int Retry)> Found =
                new List<(LevelDefinition, StateSpaceAnalysis, PuzzleInsight, PuzzleQualityReport, ulong, int)>();
            public int Evaluations;
            public Dictionary<string, int> Misses = new Dictionary<string, int>();
        }

        public static ulong JobSeed(ulong seed, ulong jobIndex, int retry)
            => SplitMix64.Derive(seed ^ ((ulong)(0x20 + SeedVersion) << 56), jobIndex * 128UL + (ulong)retry);

        public static BalancedBuild Build(IReadOnlyList<LevelDefinition> previous, ulong seed = DefaultSeed, Action<string> log = null)
        {
            var watch = Stopwatch.StartNew();
            var build = new BalancedBuild();
            if (previous.Count != ExpandedCampaign.LevelCount) { build.Message = "expected the 160-level Phase 10 pack"; return build; }

            // 1. Audit the shipped campaign.
            var before = new QualityAuditRow[previous.Count];
            Parallel.For(0, previous.Count, i => before[i] = QualityAudit.AnalyzeLevel(previous[i], i, ExpandedCampaign.WorldOf(i + 1)));
            QualityAudit.Finish(before);
            build.Previous = before.ToList();
            var weak = before.Select(Weakness).ToArray();
            // v2: old Cozy Nest slots aside, every kept puzzle lands at level 6 or later, so the position rules
            // apply to it there. Such "late-weak" levels get extra searches with their own seeds, so every other
            // slot keeps the v1 search (and result).
            var lateWeak = before.Select(r => r.Index >= 5 && Weakness(r).Count == 0 ? Weakness(r, true) : new List<string>()).ToArray();
            ulong lateSeed = 100_000;
            log?.Invoke("audited Phase 10: " + weak.Count(w => w.Count > 0) + " weak levels");

            var slots = new BalancedLevel[LevelCount];
            var jobs = new List<Job>();
            var pending = new Dictionary<string, List<BalancedLevel>>(); // mechanic World → unordered levels
            var usedPrevious = new HashSet<int>();

            // 2. Opening: authored boards and kept basics.
            foreach (var a in Onboarding)
            {
                var level = LevelText.Parse(1, "authored", a.Rows);
                slots[a.Level - 1] = FinishAuthored(level, a.Level, a.Instruction, a.Stage, a.Intent, out string problem);
                if (problem != null) { build.Message = problem; return build; }
            }
            foreach (var k in KeptOpening)
            {
                usedPrevious.Add(k.Old - 1);
                slots[k.New - 1] = Keep(previous[k.Old - 1], before[k.Old - 1], k.New - 1, k.Stage, k.Intent);
            }

            // 3. Mechanic Worlds (Frosted Pond … Prism Observatory): stage-structured, ordered by challenge afterwards.
            ulong jobIndex = 0;
            foreach (var world in Worlds.Where(w => MechanicWorlds.ContainsKey(w.Id)))
            {
                var list = new List<BalancedLevel>();
                pending[world.Id] = list;
                int fixedCount = 0;
                for (int n = world.First; n <= world.Last; n++) if (slots[n - 1] != null) fixedCount++;
                // Discovery / Application for Worlds whose openers are not authored above.
                if (world.Id != "frosted-pond")
                {
                    if (slots[world.First - 1] == null)
                        jobs.Add(new Job { Slot = world.First - 1, WorldId = world.Id, Stage = LearningStage.Discovery, Target = StageTarget(world.Id, LearningStage.Discovery), Floors = FloorsFor(LearningStage.Discovery), SeedIndex = jobIndex++ });
                    jobs.Add(new Job { Slot = world.First, WorldId = world.Id, Stage = LearningStage.Application, Target = StageTarget(world.Id, LearningStage.Application), Floors = FloorsFor(LearningStage.Application), SeedIndex = jobIndex++ });
                    fixedCount = world.Id == "ember-hollow" ? 2 : 2;
                }
                int open = world.Last - world.First + 1 - fixedCount;
                // Phase 10 levels of this World (by old range) that pass, or can be improved.
                var oldRange = OldRange(world.Id);
                var replaceReasons = new List<(int Old, string Why)>();
                var lateReasons = new List<(int Old, string Why)>();
                for (int old = oldRange.First - 1; old < oldRange.Last && list.Count < open; old++)
                {
                    if (usedPrevious.Contains(old) || IsOldIntro(old)) continue;
                    if (lateWeak[old].Count > 0) { lateReasons.Add((old, string.Join(", ", lateWeak[old]) + " at its new position")); continue; }
                    if (weak[old].Count == 0)
                    {
                        usedPrevious.Add(old);
                        list.Add(Keep(previous[old], before[old], -1, LearningStage.Reasoning, ""));
                        continue;
                    }
                    var improved = TryImprove(previous[old], before[old], LearningStage.Reasoning, world.Id);
                    if (improved != null) { usedPrevious.Add(old); improved.Weakness = string.Join(", ", weak[old]); list.Add(improved); }
                    else replaceReasons.Add((old, string.Join(", ", weak[old])));
                }
                int late = Math.Min(lateReasons.Count, open - list.Count);
                int searches = open - list.Count - late;
                for (int s = 0; s < searches; s++)
                {
                    var stage = s == searches - 1 && searches >= 2 ? LearningStage.Mastery
                        : s < (searches + 1) / 2 ? LearningStage.Reasoning : LearningStage.Combination;
                    jobs.Add(new Job { WorldId = world.Id, Stage = stage, Target = StageTarget(world.Id, stage), Floors = FloorsFor(stage),
                        Previous = s < replaceReasons.Count ? replaceReasons[s].Old : -1,
                        Weakness = s < replaceReasons.Count ? replaceReasons[s].Why : "", SeedIndex = jobIndex++ });
                }
                for (int s = 0; s < late; s++)
                    jobs.Add(new Job { WorldId = world.Id, Stage = LearningStage.Reasoning, Target = StageTarget(world.Id, LearningStage.Reasoning),
                        Floors = FloorsFor(LearningStage.Reasoning), Previous = lateReasons[s].Old, Weakness = lateReasons[s].Why, SeedIndex = lateSeed++ });
            }

            // 4. Combination Worlds (64–160): keep strong levels in place, improve or replace weak ones with the slot's own target.
            for (int i = 63; i < LevelCount; i++)
            {
                var world = WorldOf(i + 1);
                if (weak[i].Count == 0)
                {
                    usedPrevious.Add(i);
                    slots[i] = Keep(previous[i], before[i], i, LearningStage.Mixed, IntentFor(world, LearningStage.Mixed));
                    continue;
                }
                var plan = ExpandedCampaign.SlotFor(i);
                var stage = plan.Breather ? LearningStage.Breather : LearningStage.Mixed;
                var improved = TryImprove(previous[i], before[i], stage, world);
                if (improved != null)
                {
                    usedPrevious.Add(i);
                    improved.Index = i;
                    improved.Weakness = string.Join(", ", weak[i]);
                    slots[i] = improved;
                    continue;
                }
                var target = plan.Kind == SlotKind.Generated ? plan.Target : NeighbourTarget(i);
                target = Harden(target);
                jobs.Add(new Job { Slot = i, WorldId = world, Stage = stage, Target = target, Floors = FloorsFor(stage), Previous = i,
                    Weakness = string.Join(", ", weak[i]), SeedIndex = jobIndex++ });
            }

            // 5. Searches in parallel (each job's candidates depend only on its seeds), then deterministic picking.
            log?.Invoke("searching " + jobs.Count + " slots");
            int finished = 0;
            Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, job =>
            {
                // Tiny Discovery boards rarely pass the floors; give them a longer search.
                int retries = job.Stage == LearningStage.Discovery ? MaxRetries * 4 : MaxRetries;
                for (int retry = 0; retry < retries && job.Found.Count < CandidatesPerSlot; retry++)
                {
                    ulong s = JobSeed(seed, job.SeedIndex, retry);
                    var r = QualitySearch.Search(s, job.Target);
                    job.Evaluations += r.Evaluations;
                    if (!r.Success) continue;
                    var q = PuzzleQuality.Evaluate(r.Level, r.Analysis, r.Insight);
                    var miss = job.Floors.Check(q, job.Target.RequiredUse);
                    if (miss.Count > 0)
                    {
                        foreach (var m in miss)
                        {
                            string key = m.Split(' ')[0];
                            job.Misses[key] = job.Misses.TryGetValue(key, out int c) ? c + 1 : 1;
                        }
                        continue;
                    }
                    if (!BfsSolver.VerifySolution(r.Level, r.Analysis.Solution)) continue;
                    job.Found.Add((r.Level, r.Analysis, r.Insight, q, s, retry));
                }
                int n = System.Threading.Interlocked.Increment(ref finished);
                if (n % 10 == 0) log?.Invoke("  searched " + n + "/" + jobs.Count + " (" + watch.Elapsed.TotalSeconds.ToString("0") + " s)");
            });
            build.Searches = jobs.Sum(j => j.Evaluations);

            var registry = new DuplicateRegistry();
            var accepted = new List<LevelDefinition>();
            foreach (var s in slots) if (s != null) { registry.TryAdd(s.Level); accepted.Add(s.Level); }
            foreach (var list in pending.Values) foreach (var l in list) { registry.TryAdd(l.Level); accepted.Add(l.Level); }

            var failures = new List<string>();
            foreach (var job in jobs)
            {
                BalancedLevel chosen = null;
                foreach (var c in job.Found)
                {
                    if (registry.Contains(c.Level) || registry.ContainsNearDuplicate(c.Level)) continue;
                    if (accepted.Any(a => PuzzleQuality.Similarity(a, c.Level) >= QualityAudit.SimilarThreshold)) continue;
                    var level = ExpandedCampaign.Finalize(c.Level, c.Analysis, c.Insight, c.Seed,
                        "balanced v" + Version + " " + job.Stage + " " + job.Target.Describe() + " retry=" + c.Retry,
                        job.Stage == LearningStage.Discovery && DiscoveryText.TryGetValue(job.WorldId, out var text) ? text : null);
                    level.Metadata.Notes = job.Previous >= 0
                        ? "Phase 11: replaces Level " + (job.Previous + 1) + " of PLOP_Pack_002 (" + job.Weakness + ")"
                        : "Phase 11: new " + job.Stage + " level for " + job.WorldId;
                    chosen = new BalancedLevel
                    {
                        Index = job.Slot, WorldId = job.WorldId, Provenance = LevelProvenance.Replaced, Stage = job.Stage, Level = level,
                        PreviousLevel = job.Previous >= 0 ? job.Previous + 1 : -1, PreviousId = job.Previous >= 0 ? previous[job.Previous].Id : null,
                        Weakness = job.Weakness, Intent = IntentFor(job.WorldId, job.Stage), Quality = c.Quality, Insight = c.Insight
                    };
                    break;
                }
                if (chosen == null)
                {
                    failures.Add(job.WorldId + " " + job.Stage + (job.Slot >= 0 ? " slot " + (job.Slot + 1) : "") + ": " + job.Found.Count +
                                 " passed · misses " + string.Join(" ", job.Misses.OrderByDescending(m => m.Value).Select(m => m.Key + "×" + m.Value)) +
                                 " — " + job.Target.Describe());
                    continue;
                }
                registry.TryAdd(chosen.Level);
                accepted.Add(chosen.Level);
                if (job.Slot >= 0) slots[job.Slot] = chosen;
                else pending[job.WorldId].Add(chosen);
            }

            if (failures.Count > 0)
            {
                build.Message = failures.Count + " slot(s) unfilled:\n  " + string.Join("\n  ", failures);
                return build;
            }

            // 6. Order each mechanic World: fixed openers, then rising challenge; the hardest closes the World.
            foreach (var world in Worlds.Where(w => MechanicWorlds.ContainsKey(w.Id)))
            {
                var ordered = pending[world.Id].OrderBy(l => l.Insight.Challenge).ThenBy(l => l.Level.Id, StringComparer.Ordinal).ToList();
                int k = 0;
                for (int n = world.First; n <= world.Last; n++)
                {
                    if (slots[n - 1] != null) continue;
                    if (k >= ordered.Count) { build.Message = "World " + world.Id + " has an empty slot at " + n; return build; }
                    var l = ordered[k++];
                    l.Index = n - 1;
                    if (l.Provenance == LevelProvenance.Kept && l.Stage == LearningStage.Reasoning)
                        l.Stage = UsesSecondary(l.Level, world.Id) ? LearningStage.Combination : LearningStage.Reasoning;
                    if (n == world.Last && l.Stage != LearningStage.Mastery) l.Stage = LearningStage.Mastery;
                    if (string.IsNullOrEmpty(l.Intent)) l.Intent = IntentFor(world.Id, l.Stage);
                    slots[n - 1] = l;
                }
            }

            // 7. Limited Moves challenges and names; strip old limits elsewhere.
            for (int i = 0; i < LevelCount; i++)
            {
                var b = slots[i];
                b.Index = i;
                b.WorldId = WorldOf(i + 1);
                int slack = ChallengeSlack(i + 1);
                var level = b.Level;
                if (level.MoveLimit > 0 && slack < 0) level = Relabel(level.WithMoveLimit(0));
                if (slack >= 0)
                {
                    level = Relabel(level.WithMoveLimit(level.OptimalMoves + slack));
                    b.MoveLimitSlack = slack;
                }
                if (!ReferenceEquals(level, b.Level)) b.LimitChanged = true;
                b.Level = level.WithMetadata(level.Metadata, name: "Level " + (i + 1));
                if (b.Level.Metadata?.Difficulty != null && b.Insight != null && b.Quality != null)
                    b.Level.Metadata.Difficulty.Category = QualityAudit.Band(b.Insight.Challenge, b.Quality.PlanningDepth);
            }

            build.Levels = slots.ToList();
            build.Success = true;
            build.Seconds = watch.Elapsed.TotalSeconds;
            return build;
        }

        // ------------------------------------------------------------------ Daily Puzzle pool

        public const string DailyPackId = "PLOP_Daily_002";
        public const int DailyCount = 60;

        /// <summary>Daily tiers: kinds a puzzle may use, the kind it must use, how many. A player only gets tiers they have learned.</summary>
        public static readonly (MechanicSet Mechanics, MechanicSet Required, int Count)[] DailyTiers =
        {
            (MechanicSet.None, MechanicSet.None, 20),
            (MechanicSet.Frost, MechanicSet.Frost, 10),
            (MechanicSet.Breeze | MechanicSet.Frost, MechanicSet.Breeze, 10),
            (MechanicSet.Lava | MechanicSet.Frost | MechanicSet.Breeze, MechanicSet.Lava, 10),
            (MechanicSet.Loops | MechanicSet.Lava | MechanicSet.Frost, MechanicSet.Loops, 10),
        };

        public static QualityTarget DailyTarget(int tier, int k)
        {
            var t = new QualityTarget
            {
                Width = k % 2 == 0 ? 5 : 6, Height = 6, Plops = k % 3 == 2 ? 3 : 2, MinOptimal = 7, MaxOptimal = 13,
                RequireGreedyFail = true, MinTraps = 1, MaxRandomSolve = 0.05, MaxRepetition = 0.4, MaxUnusedSpace = 0.5,
                Restarts = 30, StepsPerRestart = 180, SolverMaxStates = 500_000, MaxTiles = 5, MaxFans = 2,
                Mechanics = DailyTiers[tier].Mechanics, RequiredUse = DailyTiers[tier].Required
            };
            t.MinMechanicSteps = (t.RequiredUse & ~MechanicSet.Lava) != MechanicSet.None ? 1 : 0;
            return t;
        }

        /// <summary>
        /// Rebuilds the Daily pool: strong classic puzzles of the previous pool are kept (same ids), the rest of the
        /// classic tier and every mechanic tier are searched with the Phase 11 floors, deduplicated against the
        /// campaign. Returned in tier order.
        /// </summary>
        public static List<LevelDefinition> BuildDaily(IReadOnlyList<LevelDefinition> previousDaily, IReadOnlyList<LevelDefinition> campaign,
            ulong seed, Action<string> log, out string problem)
        {
            problem = null;
            var rows = new QualityAuditRow[previousDaily.Count];
            Parallel.For(0, previousDaily.Count, i => rows[i] = QualityAudit.AnalyzeLevel(previousDaily[i], i, "daily"));
            var keep = new List<LevelDefinition>();
            for (int i = 0; i < rows.Length && keep.Count < DailyTiers[0].Count; i++)
                if (Weakness(rows[i], true).Count == 0 && previousDaily[i].Tiles.Count == 0) keep.Add(previousDaily[i]);
            log?.Invoke("daily: keeping " + keep.Count + " of " + previousDaily.Count + " classic puzzles");

            var jobs = new List<Job>();
            ulong index = 1000;
            for (int tier = 0; tier < DailyTiers.Length; tier++)
            {
                int need = DailyTiers[tier].Count - (tier == 0 ? keep.Count : 0);
                for (int k = 0; k < need; k++)
                    jobs.Add(new Job { WorldId = "daily-" + tier, Stage = LearningStage.Mixed, Target = DailyTarget(tier, k), Floors = FloorsFor(LearningStage.Mixed), SeedIndex = index++ });
            }
            Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, job =>
            {
                for (int retry = 0; retry < MaxRetries && job.Found.Count < CandidatesPerSlot; retry++)
                {
                    ulong s = JobSeed(seed, job.SeedIndex, retry);
                    var r = QualitySearch.Search(s, job.Target);
                    if (!r.Success) continue;
                    var q = PuzzleQuality.Evaluate(r.Level, r.Analysis, r.Insight);
                    if (job.Floors.Check(q, job.Target.RequiredUse).Count > 0) continue;
                    if (!BfsSolver.VerifySolution(r.Level, r.Analysis.Solution)) continue;
                    job.Found.Add((r.Level, r.Analysis, r.Insight, q, s, retry));
                }
            });
            var registry = new DuplicateRegistry();
            var accepted = new List<LevelDefinition>();
            foreach (var l in campaign) { registry.TryAdd(l); accepted.Add(l); }
            var pool = new List<LevelDefinition>();
            foreach (var l in keep) { registry.TryAdd(l); pool.Add(l); }
            var failures = new List<string>();
            foreach (var job in jobs)
            {
                LevelDefinition chosen = null;
                foreach (var c in job.Found)
                {
                    if (registry.Contains(c.Level) || registry.ContainsNearDuplicate(c.Level)) continue;
                    if (accepted.Any(a => PuzzleQuality.Similarity(a, c.Level) >= QualityAudit.SimilarThreshold)) continue;
                    if (pool.Any(a => PuzzleQuality.Similarity(a, c.Level) >= QualityAudit.SimilarThreshold)) continue;
                    chosen = ExpandedCampaign.Finalize(c.Level, c.Analysis, c.Insight, c.Seed, "daily balanced v" + Version + " " + job.Target.Describe() + " retry=" + c.Retry, null);
                    chosen.Metadata.Difficulty.Category = QualityAudit.Band(c.Insight.Challenge, c.Quality.PlanningDepth);
                    chosen.Metadata.Notes = "Phase 11 Daily pool, tier " + job.WorldId;
                    break;
                }
                if (chosen == null) { failures.Add(job.WorldId + ": " + job.Found.Count + " passed — " + job.Target.Describe()); continue; }
                registry.TryAdd(chosen);
                pool.Add(chosen);
            }
            if (failures.Count > 0) { problem = failures.Count + " daily slot(s) unfilled:\n  " + string.Join("\n  ", failures); return null; }
            for (int i = 0; i < pool.Count; i++) pool[i] = pool[i].WithMetadata(pool[i].Metadata, name: "Daily " + (i + 1));
            return pool;
        }

        private static (int First, int Last) OldRange(string worldId)
        {
            foreach (var w in ExpandedCampaign.Worlds) if (w.Id == worldId) return (w.First, w.Last);
            return (1, 0);
        }

        /// <summary>Phase 10 mechanic introductions (replaced by Phase 11 onboarding, except Lava's, which is kept).</summary>
        private static bool IsOldIntro(int oldIndex) => oldIndex == 6 || oldIndex == 7 || oldIndex == 20 || oldIndex == 21 ||
                                                         oldIndex == 34 || oldIndex == 35 || oldIndex == 48;

        private static bool UsesSecondary(LevelDefinition level, string worldId)
        {
            var m = MechanicWorlds[worldId];
            foreach (var t in level.Tiles)
            {
                var set = t.Kind == TileKind.Frost ? MechanicSet.Frost : t.Kind == TileKind.Lava ? MechanicSet.Lava
                    : t.Kind == TileKind.Fan ? MechanicSet.Breeze : MechanicSet.Loops;
                if ((set & m.Main) == 0) return true;
            }
            return level.Plops.Count > 1 && m.Second == MechanicSet.None;
        }

        private static QualityTarget NeighbourTarget(int index)
        {
            var world = ExpandedCampaign.WorldOf(index + 1);
            for (int d = 1; d < 8; d++)
            {
                foreach (int j in new[] { index - d, index + d })
                {
                    if (j < 0 || j >= LevelCount || ExpandedCampaign.WorldOf(j + 1) != world) continue;
                    var s = ExpandedCampaign.SlotFor(j);
                    if (s.Kind == SlotKind.Generated) return s.Target;
                }
            }
            return ExpandedCampaign.SlotFor(index - 1).Target;
        }

        /// <summary>Phase 10 late targets with the Phase 11 anti-repetition limit.</summary>
        private static QualityTarget Harden(QualityTarget t)
        {
            var h = t.Clone();
            h.MaxRepetition = Math.Min(h.MaxRepetition, 0.4);
            return h;
        }

        private static LevelDefinition Relabel(LevelDefinition level)
        {
            string id = LevelCanonicalizer.CanonicalId(level);
            return level.WithMetadata(level.Metadata, id, id);
        }

        private static BalancedLevel Keep(LevelDefinition level, QualityAuditRow row, int index, LearningStage stage, string intent)
            => new BalancedLevel
            {
                Index = index, WorldId = index >= 0 ? WorldOf(index + 1) : "", Provenance = LevelProvenance.Kept, Stage = stage, Level = level,
                PreviousLevel = row.Index + 1, PreviousId = level.Id, Intent = intent, Quality = row.Quality, Insight = row.Insight
            };

        private static BalancedLevel FinishAuthored(LevelDefinition layout, int number, string instruction, LearningStage stage, string intent, out string problem)
        {
            problem = null;
            var analysis = StateSpaceAnalyzer.AnalyzeWithGraph(layout);
            if (!analysis.Solvable || !BfsSolver.VerifySolution(layout, analysis.Solution))
            {
                problem = "authored level " + number + " is not solvable";
                return null;
            }
            var insight = PuzzleInsight.Analyze(layout, analysis);
            var level = ExpandedCampaign.Finalize(layout, analysis, insight, null, "authored (Phase 11 onboarding)", instruction);
            level.Metadata.Author = "Phase 11 onboarding";
            level.Metadata.Notes = "Phase 11: " + intent;
            return new BalancedLevel
            {
                Index = number - 1, WorldId = WorldOf(number), Provenance = LevelProvenance.Authored, Stage = stage, Level = level,
                Intent = intent, Insight = insight, Quality = PuzzleQuality.Evaluate(level, analysis, insight)
            };
        }

        /// <summary>Removes decorative mechanic kinds and idle tiles; returns the improved level only when it passes the floors.</summary>
        private static BalancedLevel TryImprove(LevelDefinition level, QualityAuditRow row, LearningStage stage, string worldId)
        {
            if (!row.Analysed || (row.Quality.DecorativeKinds == 0 && row.Quality.IdleTiles == 0)) return null;
            var route = new HashSet<int>(row.Insight.RouteCells);
            var decorative = new HashSet<TileKind>(row.Quality.Mechanics.Where(m => m.Role == MechanicRole.Decorative).Select(m => m.Kind));
            var keep = new List<MechanicTile>();
            foreach (var t in level.Tiles)
            {
                int cell = level.CellIndex(t.Position);
                bool idle = t.Kind != TileKind.Lava && t.Kind != TileKind.Portal && !route.Contains(cell);
                if (decorative.Contains(t.Kind) || idle) continue;
                keep.Add(t);
            }
            if (keep.Count == level.Tiles.Count || keep.Count == 0) return null;
            var candidate = level.WithTiles(keep).WithMoveLimit(0);
            if (!LevelValidator.Validate(candidate).IsValid) return null;
            var analysis = StateSpaceAnalyzer.AnalyzeWithGraph(candidate);
            if (!analysis.Solvable) return null;
            var insight = PuzzleInsight.Analyze(candidate, analysis);
            var q = PuzzleQuality.Evaluate(candidate, analysis, insight);
            var required = MechanicWorlds.TryGetValue(worldId, out var m) ? m.Main : MechanicSet.None;
            if (FloorsFor(stage).Check(q, required).Count > 0) return null;
            var finished = ExpandedCampaign.Finalize(candidate, analysis, insight, level.Metadata?.Seed,
                (level.Metadata?.GeneratorParams ?? "") + " · improved by balanced v" + Version, level.Instruction);
            finished.Metadata.Notes = "Phase 11: Level " + (row.Index + 1) + " of PLOP_Pack_002 with " + (level.Tiles.Count - keep.Count) +
                                      " decorative/idle tile(s) removed";
            return new BalancedLevel
            {
                Index = -1, WorldId = worldId, Provenance = LevelProvenance.Improved, Stage = stage, Level = finished,
                PreviousLevel = row.Index + 1, PreviousId = level.Id, Quality = q, Insight = insight,
                Intent = "Same puzzle without tiles that never mattered."
            };
        }
    }
}
