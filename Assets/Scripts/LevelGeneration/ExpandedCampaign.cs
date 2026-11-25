using System;
using System.Collections.Generic;
using System.Diagnostics;
using Plop.Core;
using Plop.Solver;

namespace Plop.Generation
{
    /// <summary>How a campaign slot is filled.</summary>
    public enum SlotKind { Authored, Generated, Classic }

    /// <summary>One slot of the 160-level campaign.</summary>
    public sealed class SlotPlan
    {
        public int Index;                 // zero-based
        public SlotKind Kind;
        public string WorldId;
        public string[] Rows;             // authored boards (LevelText)
        public int FanPower = 1;          // authored fans
        public string Instruction;        // short tutorial line, authored intros only
        public QualityTarget Target;      // generated slots
        public int ClassicSource = -1;    // Pack_001 index for classic slots
        public double ClassicChallenge;   // desired challenge for classic picks
        public bool Challenge;            // Limited Moves: limit = optimal + ChallengeSlack
        public bool Breather;
    }

    /// <summary>
    /// Phase 10 campaign: 160 levels over nine Worlds with fast onboarding. Each mechanic is taught by an
    /// authored demonstration, then applied and challenged by generated puzzles that must actually use it;
    /// later Worlds combine mechanics in difficulty waves and mix in the strongest Phase 8 pure-logic
    /// puzzles (their ids, and therefore players' records, are unchanged). Every level is solver-verified.
    /// </summary>
    public static class ExpandedCampaign
    {
        public const int Version = 1;
        public const int LevelCount = 160;
        public const string PackId = "PLOP_Pack_002";
        public const int ChallengeSlack = 2;
        public const int MaxSlotRetries = 14;

        /// <summary>World id → one-based level range.</summary>
        public static readonly (string Id, int First, int Last)[] Worlds =
        {
            ("cozy-nest", 1, 6),
            ("frosted-pond", 7, 20),
            ("breezy-meadow", 21, 34),
            ("ember-hollow", 35, 48),
            ("prism-observatory", 49, 63),
            ("dewdrop-garden", 64, 85),
            ("rainy-window", 86, 108),
            ("twilight-attic", 109, 133),
            ("moonlit-clouds", 134, 160),
        };

        /// <summary>Limited Moves challenges (one-based level numbers).</summary>
        public static readonly int[] ChallengeLevels = { 30, 60, 90, 120, 150 };

        public static string WorldOf(int level)
        {
            foreach (var w in Worlds) if (level >= w.First && level <= w.Last) return w.Id;
            return Worlds[Worlds.Length - 1].Id;
        }

        // Phase 8 tutorial boards kept verbatim (same ids) for levels 1–5.
        private static readonly (string[] Rows, string Instruction)[] Basics =
        {
            (new[] { "....", "....", "Rr..", "...." }, "Swipe the Plop into its matching Nest."),
            (new[] { "...r", "....", ".R..", "...." }, null),
            (new[] { "....", "R.r.", "B.b.", "...." }, "One swipe moves every awake Plop."),
            (new[] { "..rR", "..B#", "....", "...b" }, null),
            (new[] { "..b", "Rr.", "#B#" }, null),
        };

        public static List<SlotPlan> Plan()
        {
            var plan = new List<SlotPlan>();
            for (int i = 0; i < LevelCount; i++) plan.Add(SlotFor(i));
            return plan;
        }

        public static SlotPlan SlotFor(int index)
        {
            int level = index + 1;
            var slot = new SlotPlan { Index = index, WorldId = WorldOf(level), Challenge = Array.IndexOf(ChallengeLevels, level) >= 0 };
            if (level <= 5)
            {
                slot.Kind = SlotKind.Authored;
                slot.Rows = Basics[index].Rows;
                slot.Instruction = Basics[index].Instruction;
                return slot;
            }
            switch (level)
            {
                case 6: return Classic(slot, 24);
                // ---- Frosted Pond ----
                case 7: return Authored(slot, "Frost: Plops slide until something stops them.", 1, ".....", "R~..r", ".....");
                case 8: return Authored(slot, null, 1, "...r.", "R~..#", ".....");
                // ---- Breezy Meadow ----
                case 21: return Authored(slot, "Breeze fans push Plops their way.", 3, ".r..", "....", "....", "R^..");
                case 22: return Authored(slot, null, 2, "...r", "....", "....", "R^..");
                // ---- Ember Hollow ----
                case 35: return Authored(slot, "Lava is too hot! Keep every Plop out.", 1, "R!r", "...");
                case 36: return Authored(slot, null, 1, "...r.", "R~..!", ".....");
                // ---- Prism Observatory ----
                case 49: return Authored(slot, "Loops connect: in one, out the other.", 1, "R1..r", "....1");
            }
            return Generated(slot, level);
        }

        private static SlotPlan Authored(SlotPlan slot, string instruction, int fanPower, params string[] rows)
        {
            slot.Kind = SlotKind.Authored;
            slot.Rows = rows;
            slot.FanPower = fanPower;
            slot.Instruction = instruction;
            return slot;
        }

        private static SlotPlan Classic(SlotPlan slot, double challenge)
        {
            slot.Kind = SlotKind.Classic;
            slot.ClassicChallenge = challenge;
            return slot;
        }

        private static readonly MechanicSet[] Pairs =
        {
            MechanicSet.Frost | MechanicSet.Breeze, MechanicSet.Breeze | MechanicSet.Lava, MechanicSet.Loops | MechanicSet.Frost,
            MechanicSet.Lava | MechanicSet.Frost, MechanicSet.Loops | MechanicSet.Breeze, MechanicSet.Loops | MechanicSet.Lava
        };

        private static readonly MechanicSet[] Triples =
        {
            MechanicSet.Frost | MechanicSet.Breeze | MechanicSet.Lava, MechanicSet.Loops | MechanicSet.Frost | MechanicSet.Lava,
            MechanicSet.Loops | MechanicSet.Breeze | MechanicSet.Frost, MechanicSet.Loops | MechanicSet.Breeze | MechanicSet.Lava
        };

        private static SlotPlan Generated(SlotPlan slot, int level)
        {
            var world = Array.Find(Worlds, w => w.Id == slot.WorldId);
            int inWorld = level - world.First;               // 0-based position inside the World
            int wave = inWorld % 5;                           // 0,1,2 build · 3 peak · 4 breather
            bool peak = wave == 3, breather = wave == 4;
            slot.Breather = breather;
            double ramp = breather ? -5 : wave * 1.5 + (peak ? 3 : 0); // breathers sit clearly below the wave
            double progress = (double)inWorld / Math.Max(1, world.Last - world.First);
            var t = new QualityTarget { MaxRandomSolve = 0.05, RequireGreedyFail = !breather, Restarts = 30, StepsPerRestart = 180 };

            switch (slot.WorldId)
            {
                case "frosted-pond":
                    t.Mechanics = MechanicSet.Frost; t.RequiredUse = MechanicSet.Frost;
                    t.Width = 5; t.Height = level < 13 ? 5 : 6; t.Plops = level < 11 ? 1 : 2;
                    t.MinOptimal = level < 11 ? 4 : 6; t.MaxOptimal = level < 11 ? 8 : 11; t.MinMechanicSteps = level < 11 ? 1 : 2;
                    t.MinChallenge = 12 + 14 * progress + ramp; t.MaxChallenge = t.MinChallenge + 16;
                    t.MaxUnusedSpace = 0.6; t.MinTraps = level > 14 && !breather ? 1 : 0;
                    break;
                case "breezy-meadow":
                    t.Mechanics = level > 26 ? MechanicSet.Breeze | MechanicSet.Frost : MechanicSet.Breeze; t.RequiredUse = MechanicSet.Breeze;
                    t.Width = 5; t.Height = 6; t.Plops = level < 25 ? 1 : 2;
                    t.MinOptimal = 5; t.MaxOptimal = 11; t.MinMechanicSteps = 2;
                    t.MinChallenge = 16 + 14 * progress + ramp; t.MaxChallenge = t.MinChallenge + 16;
                    t.MaxUnusedSpace = 0.6; t.MinTraps = level > 28 && !breather ? 1 : 0;
                    break;
                case "ember-hollow":
                    t.Mechanics = level > 40 ? MechanicSet.Lava | MechanicSet.Frost | MechanicSet.Breeze : MechanicSet.Lava | MechanicSet.Frost;
                    t.RequiredUse = MechanicSet.Lava;
                    t.Width = level % 2 == 0 ? 6 : 5; t.Height = 6; t.Plops = level < 39 ? 1 : 2;
                    t.MinOptimal = 6; t.MaxOptimal = 12; t.MinMechanicSteps = level > 40 ? 1 : 0;
                    t.MinChallenge = 20 + 14 * progress + ramp; t.MaxChallenge = t.MinChallenge + 16;
                    t.MaxUnusedSpace = 0.55; t.MinTraps = breather ? 0 : 1;
                    break;
                case "prism-observatory":
                    t.Mechanics = level > 55 ? MechanicSet.Loops | MechanicSet.Frost | MechanicSet.Lava : MechanicSet.Loops;
                    t.RequiredUse = MechanicSet.Loops;
                    t.Width = 5; t.Height = 6; t.Plops = level < 52 ? 1 : 2;
                    t.MinOptimal = level < 52 ? 3 : 6; t.MaxOptimal = level < 52 ? 7 : 12; t.MinMechanicSteps = 1;
                    t.MinChallenge = (level < 52 ? 12 : 22) + 14 * progress + ramp; t.MaxChallenge = t.MinChallenge + 16;
                    t.MaxUnusedSpace = 0.6; t.MinTraps = level > 54 && !breather ? 1 : 0;
                    break;
                case "dewdrop-garden":
                    if (inWorld % 7 == 6) return Classic(slot, 30 + 10 * progress);
                    t.Mechanics = Pairs[inWorld % Pairs.Length]; t.RequiredUse = t.Mechanics;
                    t.Width = level % 2 == 0 ? 6 : 5; t.Height = 6; t.Plops = peak ? 3 : 2;
                    t.MinOptimal = 7; t.MaxOptimal = 13; t.MinMechanicSteps = 2;
                    t.MinChallenge = 28 + 12 * progress + ramp; t.MaxChallenge = t.MinChallenge + 16;
                    t.MinTraps = breather ? 0 : 1; t.MinCounterIntuitive = breather ? 0 : 1;
                    break;
                case "rainy-window":
                    if (inWorld % 7 == 6) return Classic(slot, 36 + 10 * progress);
                    t.Mechanics = Pairs[(inWorld + 2) % Pairs.Length] | (peak ? MechanicSet.Frost : MechanicSet.None);
                    t.RequiredUse = Pairs[(inWorld + 2) % Pairs.Length];
                    t.Width = 6; t.Height = 6; t.Plops = breather ? 2 : 3;
                    t.MinOptimal = 8; t.MaxOptimal = 14; t.MinMechanicSteps = 2;
                    t.MinChallenge = 34 + 12 * progress + ramp; t.MaxChallenge = t.MinChallenge + 18;
                    t.MinTraps = breather ? 1 : 2; t.MinCounterIntuitive = breather ? 1 : 2; t.MaxRandomSolve = 0.02;
                    t.SolverMaxStates = 500_000;
                    break;
                case "twilight-attic":
                    if (inWorld % 7 == 6) return Classic(slot, 42 + 10 * progress);
                    t.Mechanics = Triples[inWorld % Triples.Length]; t.RequiredUse = Pairs[(inWorld * 5) % Pairs.Length] & t.Mechanics;
                    if (t.RequiredUse == MechanicSet.None) t.RequiredUse = t.Mechanics & (MechanicSet.Loops | MechanicSet.Frost);
                    t.Width = 6; t.Height = level % 2 == 0 ? 7 : 6; t.Plops = breather ? 2 : 3;
                    t.MinOptimal = 9; t.MaxOptimal = 15; t.MinMechanicSteps = 3;
                    t.MinChallenge = 40 + 12 * progress + ramp; t.MaxChallenge = t.MinChallenge + 18;
                    t.MinTraps = breather ? 1 : 2; t.MinCounterIntuitive = 2; t.MaxRandomSolve = 0.01;
                    t.SolverMaxStates = 700_000;
                    break;
                default: // moonlit-clouds: expert combinations
                    if (inWorld % 7 == 6) return Classic(slot, 48 + 10 * progress);
                    bool all = peak || inWorld % 5 == 1;
                    t.Mechanics = all ? MechanicSet.All : Triples[inWorld % Triples.Length];
                    t.RequiredUse = all ? MechanicSet.Frost | MechanicSet.Loops | MechanicSet.Lava : Triples[(inWorld + 1) % Triples.Length] & t.Mechanics;
                    if (t.RequiredUse == MechanicSet.None) t.RequiredUse = t.Mechanics & (MechanicSet.Loops | MechanicSet.Breeze);
                    t.Width = 6; t.Height = 7; t.Plops = breather ? 2 : 3;
                    t.MinOptimal = 10; t.MaxOptimal = 17; t.MinMechanicSteps = 3;
                    t.MinChallenge = 46 + 14 * progress + ramp; t.MaxChallenge = t.MinChallenge + 20;
                    t.MinTraps = 2; t.MinCounterIntuitive = breather ? 2 : 3; t.MaxRandomSolve = 0.01;
                    t.MaxTiles = 7; t.SolverMaxStates = 900_000;
                    break;
            }
            if (t.Plops == 1)
            {
                // One Plop has few states; its puzzles are about reading the mechanic, not juggling.
                t.MinChallenge = Math.Min(t.MinChallenge, 18 + ramp * 0.5);
                t.MaxChallenge = t.MinChallenge + 18;
                t.MinTraps = Math.Min(t.MinTraps, 1);
            }
            slot.Kind = SlotKind.Generated;
            slot.Target = t;
            return slot;
        }

        public static ulong SlotSeed(ulong seed, int index, int retry)
            => SplitMix64.Derive(seed ^ ((ulong)(0x10 + Version) << 56), (ulong)index * 64UL + (ulong)retry);

        /// <summary>Search for a generated slot (retry 0 depends only on seed and slot, so it can be precomputed in parallel).</summary>
        public static QualitySearchResult SearchSlotRaw(ulong seed, int index, int retry)
            => QualitySearch.Search(SlotSeed(seed, index, retry), SlotFor(index).Target);

        /// <summary>
        /// Builds the campaign in order. <paramref name="classicPack"/> supplies the Phase 8 levels for classic slots.
        /// Duplicates are rejected in campaign order and retried with the next derived seed. Stops at the first
        /// slot that cannot be filled (never weakens the target).
        /// </summary>
        public static BatchResult Generate(ulong seed, IReadOnlyList<LevelDefinition> classicPack,
            Func<int, QualitySearchResult> precomputed = null, Action<int> progress = null)
        {
            var watch = Stopwatch.StartNew();
            var registry = new DuplicateRegistry();
            var result = new BatchResult();
            var usedClassic = new HashSet<int>();

            for (int i = 0; i < LevelCount; i++)
            {
                progress?.Invoke(i);
                var slot = SlotFor(i);
                GenerationResult generated;
                if (slot.Kind == SlotKind.Authored) generated = BuildAuthored(slot, registry);
                else if (slot.Kind == SlotKind.Classic) generated = PickClassic(slot, classicPack, usedClassic, registry);
                else
                {
                    double? ceiling = slot.Breather && result.Levels.Count > 0
                        ? result.Levels[result.Levels.Count - 1].Metadata?.Difficulty?.Score : null;
                    generated = SearchSlot(seed, slot, registry, precomputed, ceiling);
                }
                result.Stats.RecordGeneration(generated);
                if (!generated.Success)
                {
                    result.Stats.StoppedByLimit = true;
                    result.Message = "slot " + (i + 1) + ": " + generated.Message;
                    break;
                }
                var level = generated.Level;
                if (slot.Challenge && level.OptimalMoves > 0) level = MakeChallenge(level);
                result.Levels.Add(level.WithMetadata(level.Metadata, name: "Level " + (i + 1)));
                result.Stats.RecordAcceptance(level);
            }
            result.Stats.ElapsedMs = watch.Elapsed.TotalMilliseconds;
            return result;
        }

        /// <summary>Limited Moves: optimal + slack, verified solvable within the limit; the limit is part of the id.</summary>
        public static LevelDefinition MakeChallenge(LevelDefinition level)
        {
            var limited = level.WithMoveLimit(level.OptimalMoves + ChallengeSlack);
            string id = LevelCanonicalizer.CanonicalId(limited);
            return limited.WithMetadata(limited.Metadata, id, id);
        }

        private static GenerationResult SearchSlot(ulong seed, SlotPlan slot, DuplicateRegistry registry,
            Func<int, QualitySearchResult> precomputed, double? ceiling)
        {
            var result = new GenerationResult();
            for (int retry = 0; retry < MaxSlotRetries; retry++)
            {
                var found = retry == 0 && precomputed != null ? precomputed(slot.Index) : SearchSlotRaw(seed, slot.Index, retry);
                result.Attempts += found.Evaluations;
                if (!found.Success) { result.Rejections[(int)RejectReason.QualityMismatch]++; continue; }
                if (ceiling.HasValue && found.Insight.Challenge >= ceiling.Value) { result.Rejections[(int)RejectReason.DifficultyMismatch]++; continue; }
                if (registry.Contains(found.Level) || registry.ContainsNearDuplicate(found.Level)) { result.Rejections[(int)RejectReason.Duplicate]++; continue; }
                if (!BfsSolver.VerifySolution(found.Level, found.Analysis.Solution)) { result.Rejections[(int)RejectReason.VerificationFailed]++; continue; }
                registry.TryAdd(found.Level);
                result.Level = Finalize(found.Level, found.Analysis, found.Insight, SlotSeed(seed, slot.Index, retry),
                    "quality-search v" + QualitySearch.Version + " expanded v" + Version + " " + slot.Target.Describe() + " retry=" + retry, null);
                result.Analysis = found.Analysis;
                result.Success = true;
                return result;
            }
            result.Message = "not filled after " + MaxSlotRetries + " searches (" + slot.Target.Describe() + ")";
            return result;
        }

        private static GenerationResult BuildAuthored(SlotPlan slot, DuplicateRegistry registry)
        {
            var layout = LevelText.Parse(slot.FanPower, "authored", slot.Rows);
            var result = new GenerationResult { Attempts = 1 };
            var analysis = StateSpaceAnalyzer.AnalyzeWithGraph(layout);
            if (!analysis.Solvable || !BfsSolver.VerifySolution(layout, analysis.Solution))
            {
                result.Rejections[(int)RejectReason.VerificationFailed]++;
                result.Message = "authored level " + (slot.Index + 1) + " is not solvable";
                return result;
            }
            if (registry.Contains(layout) || registry.ContainsNearDuplicate(layout))
            {
                result.Rejections[(int)RejectReason.Duplicate]++;
                result.Message = "authored level " + (slot.Index + 1) + " duplicates an earlier level";
                return result;
            }
            registry.TryAdd(layout);
            var insight = PuzzleInsight.Analyze(layout, analysis);
            result.Level = Finalize(layout, analysis, insight, null, "authored", slot.Instruction);
            result.Analysis = analysis;
            result.Success = true;
            return result;
        }

        private static GenerationResult PickClassic(SlotPlan slot, IReadOnlyList<LevelDefinition> pack, HashSet<int> used, DuplicateRegistry registry)
        {
            var result = new GenerationResult { Attempts = 1 };
            int best = -1;
            double bestGap = double.MaxValue;
            for (int i = 10; i < (pack?.Count ?? 0); i++)
            {
                if (used.Contains(i) || pack[i].Metadata?.Difficulty == null) continue;
                if (registry.Contains(pack[i]) || registry.ContainsNearDuplicate(pack[i])) continue;
                double gap = Math.Abs(pack[i].Metadata.Difficulty.Score - slot.ClassicChallenge);
                if (gap < bestGap) { bestGap = gap; best = i; }
            }
            if (best < 0) { result.Message = "no Phase 8 level left for classic slot"; return result; }
            used.Add(best);
            var level = pack[best];
            registry.TryAdd(level);
            var analysis = StateSpaceAnalyzer.Analyze(level);
            if (!analysis.Solvable || analysis.StartDistance != level.OptimalMoves || !BfsSolver.VerifySolution(level, analysis.Solution))
            {
                result.Message = "classic level " + level.Id + " failed verification";
                return result;
            }
            var notes = level.Metadata;
            notes.Notes = "Phase 8 pure-logic level reused (was Level " + (best + 1) + " of PLOP_Pack_001); id unchanged";
            result.Level = level.WithMetadata(notes);
            result.Analysis = analysis;
            result.Success = true;
            return result;
        }

        /// <summary>Canonical id, verified shortest solution, insight metrics and the Perfect target.</summary>
        private static LevelDefinition Finalize(LevelDefinition level, StateSpaceAnalysis analysis, PuzzleInsight insight, ulong? seed,
            string parameters, string instruction)
        {
            string id = LevelCanonicalizer.CanonicalId(level);
            var difficulty = DifficultyEstimator.Estimate(level, analysis);
            difficulty.Score = insight.Challenge;
            difficulty.Category = CampaignPlan.Categorize(insight.Challenge);
            ProductionCatalog.AddInsightMetrics(difficulty, insight);
            var m = difficulty.Metrics;
            m["hazardAlternatives"] = insight.HazardAlternatives;
            m["mechanicSteps"] = insight.MechanicSteps;
            m["mechanicKindsUsed"] = insight.MechanicKindsUsed;
            m["slides"] = insight.Slides;
            m["pushes"] = insight.Pushes;
            m["teleports"] = insight.Teleports;
            var meta = new LevelMetadata
            {
                GeneratorVersion = GeneratorInfo.Version,
                Seed = seed,
                GeneratorParams = parameters,
                SolverVersion = SolverInfo.Version,
                Solution = DirectionUtil.ToMoveString(analysis.Solution),
                Difficulty = difficulty
            };
            var finished = new LevelDefinition(id, id, level.Width, level.Height, level.Walls, level.Plops, level.Nests, meta,
                instruction, analysis.StartDistance, level.Tiles, level.MoveLimit);
            return finished;
        }
    }
}
