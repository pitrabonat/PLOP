using System.Collections.Generic;
using NUnit.Framework;
using Plop.Core;
using Plop.Generation;
using Plop.Solver;

namespace Plop.Tests
{
    public class GenerationTests
    {
        private static GeneratorSettings Small(int plops = 2) => new GeneratorSettings
        {
            Width = 5, Height = 5, PlopCount = plops, WallDensity = 0.2, MaxAttempts = 200, SolverMaxStates = 200_000
        };

        [Test]
        public void SplitMix64_MatchesReferenceValues()
        {
            // Reference output of SplitMix64 for seed 0 (Vigna's splitmix64.c).
            var rng = new SplitMix64(0);
            Assert.AreEqual(0xE220A8397B1DCDAFUL, rng.NextULong());
            Assert.AreEqual(0x6E789E6AA1B965F4UL, rng.NextULong());
            Assert.AreEqual(0x06C45D188009454FUL, rng.NextULong());
        }

        [Test]
        public void SplitMix64_NextInt_InRange()
        {
            var rng = new SplitMix64(42);
            var seen = new bool[7];
            for (int i = 0; i < 1000; i++)
            {
                int v = rng.NextInt(7);
                Assert.That(v, Is.InRange(0, 6));
                seen[v] = true;
            }
            CollectionAssert.DoesNotContain(seen, false);
        }

        [Test]
        public void Generator_SameSeed_SameLevel()
        {
            var a = LevelGenerator.Generate(12345, Small());
            var b = LevelGenerator.Generate(12345, Small());
            Assert.IsTrue(a.Success, a.Diagnostics());
            Assert.AreEqual(LevelSerializer.ToJson(a.Level), LevelSerializer.ToJson(b.Level));
            Assert.AreEqual(a.Attempts, b.Attempts);
        }

        [Test]
        public void Generator_DifferentSeeds_UsuallyDiffer()
        {
            var ids = new HashSet<string>();
            for (ulong s = 1; s <= 10; s++)
            {
                var r = LevelGenerator.Generate(s, Small());
                Assert.IsTrue(r.Success, r.Diagnostics());
                ids.Add(r.Level.Id);
            }
            Assert.Greater(ids.Count, 7);
        }

        [Test]
        public void GeneratedLevels_AreValidSolvableAndReplayable([Values(1, 2, 3)] int plops)
        {
            for (ulong s = 100; s < 108; s++)
            {
                var settings = Small(plops);
                var r = LevelGenerator.Generate(s, settings);
                Assert.IsTrue(r.Success, r.Diagnostics());
                var level = r.Level;
                Assert.IsTrue(LevelValidator.Validate(level).IsValid);
                var report = LevelVerifier.Verify(level);
                Assert.IsTrue(report.IsValid, report.ToString());
                CollectionAssert.IsEmpty(report.Warnings, "stored solution must be a shortest solution");
                var moves = DirectionUtil.ParseMoveString(level.Metadata.Solution);
                Assert.IsTrue(BfsSolver.VerifySolution(level, moves));
                Assert.AreEqual(BfsSolver.Solve(level).SolutionLength, moves.Length);
                Assert.GreaterOrEqual(moves.Length, settings.MinSolutionLength);
                Assert.AreEqual(GeneratorInfo.Version, level.Metadata.GeneratorVersion);
                Assert.AreEqual(s, level.Metadata.Seed);
                Assert.AreEqual(LevelCanonicalizer.CanonicalId(level), level.Id);
                // the stored JSON round-trips
                Assert.AreEqual(LevelSerializer.ToJson(level), LevelSerializer.ToJson(LevelSerializer.FromJson(LevelSerializer.ToJson(level))));
            }
        }

        [Test]
        public void Generator_ImpossibleSettings_FailWithDiagnostics()
        {
            var settings = new GeneratorSettings { Width = 2, Height = 2, PlopCount = 1, WallDensity = 0, MaxAttempts = 25, MinSolutionLength = 10 };
            var r = LevelGenerator.Generate(7, settings);
            Assert.IsFalse(r.Success);
            Assert.IsNull(r.Level);
            Assert.AreEqual(25, r.Attempts);
            int total = 0;
            foreach (int n in r.Rejections) total += n;
            Assert.AreEqual(25, total, "every attempt must be accounted for");
            Assert.Greater(r.RejectionCount(RejectReason.Trivial), 0);
            StringAssert.Contains("FAILED", r.Diagnostics());
        }

        [Test]
        public void Generator_SearchBudget_RejectsUnverifiedCandidates()
        {
            var settings = Small(3);
            settings.SolverMaxStates = 5; // far too small to prove anything
            settings.MaxAttempts = 10;
            var r = LevelGenerator.Generate(3, settings);
            Assert.IsFalse(r.Success, "levels that cannot be verified must be rejected");
            Assert.Greater(r.RejectionCount(RejectReason.SearchBudget), 0);
        }

        [Test]
        public void Generator_TargetDifficulty_IsRespected()
        {
            var settings = Small(2);
            settings.TargetDifficulty = DifficultyCategory.Normal;
            var r = LevelGenerator.Generate(77, settings);
            Assert.IsTrue(r.Success, r.Diagnostics());
            Assert.AreEqual(DifficultyCategory.Normal, r.Level.Metadata.Difficulty.Category);
        }

        [Test]
        public void Duplicates_DetectedUnderAllSymmetriesAndRecoloring()
        {
            var level = LevelText.Parse("d",
                "R#..",
                "...r",
                "B..b");
            var registry = new DuplicateRegistry();
            Assert.IsTrue(registry.TryAdd(level));
            int baseLength = BfsSolver.Solve(level).SolutionLength;
            for (int t = 0; t < 8; t++)
            {
                var transformed = LevelCanonicalizer.TransformLevel(level, t);
                Assert.IsTrue(LevelValidator.Validate(transformed).IsValid);
                Assert.IsFalse(registry.TryAdd(transformed), "symmetry " + t + " not detected");
                // canonicalization must not change the puzzle: same shortest solution length
                Assert.AreEqual(baseLength, BfsSolver.Solve(transformed).SolutionLength, "symmetry " + t + " changed the rules");
            }
            var recolored = LevelText.Parse("d2",
                "B#..",
                "...b",
                "Y..y");
            Assert.IsFalse(registry.TryAdd(recolored), "color relabeling not detected");
            var different = LevelText.Parse("d3",
                "R...",
                "#..r",
                "B..b");
            Assert.IsTrue(registry.TryAdd(different));
        }

        [Test]
        public void Generator_WithRegistry_NeverReturnsDuplicates()
        {
            var registry = new DuplicateRegistry();
            var first = LevelGenerator.Generate(5, Small(1), registry);
            Assert.IsTrue(first.Success);
            var again = LevelGenerator.Generate(5, Small(1), registry); // same seed: first candidate is now a duplicate
            Assert.IsTrue(again.Success, again.Diagnostics());
            Assert.Greater(again.RejectionCount(RejectReason.Duplicate), 0);
            Assert.AreNotEqual(first.Level.Id, again.Level.Id);
        }

        [Test]
        public void Difficulty_DependsOnMoreThanSolutionLength()
        {
            var weights = DifficultyWeights.Default;
            var one = LevelText.Parse("a", "R....r");
            var two = LevelText.Parse("b", "R....r", "B#...b");
            var a1 = StateSpaceAnalyzer.Analyze(one);
            var a2 = StateSpaceAnalyzer.Analyze(two);
            Assert.IsTrue(a1.Solvable && a2.Solvable);
            var d1 = DifficultyEstimator.Estimate(one, a1, weights);
            var d2 = DifficultyEstimator.Estimate(two, a2, weights);
            Assert.AreNotEqual(d1.Score, d2.Score);
            Assert.Greater(d2.Score, d1.Score, "an extra Plop needing coordination must score higher");
            Assert.That(d2.Metrics.ContainsKey("coordinationMoves"));
        }

        [Test]
        public void Difficulty_CategoriesFollowThresholds()
        {
            var w = DifficultyWeights.Default;
            Assert.AreEqual(DifficultyCategory.Tutorial, DifficultyEstimator.Categorize(w.CategoryThresholds[0] - 0.01, w));
            Assert.AreEqual(DifficultyCategory.Easy, DifficultyEstimator.Categorize(w.CategoryThresholds[0], w));
            Assert.AreEqual(DifficultyCategory.Normal, DifficultyEstimator.Categorize(w.CategoryThresholds[1], w));
            Assert.AreEqual(DifficultyCategory.Hard, DifficultyEstimator.Categorize(w.CategoryThresholds[2], w));
            Assert.AreEqual(DifficultyCategory.Expert, DifficultyEstimator.Categorize(w.CategoryThresholds[3] + 50, w));
        }

        [Test]
        public void Batch_ProducesUniqueVerifiedLevels()
        {
            var settings = new BatchSettings { TargetCount = 12, BaseSeed = 99, MaxCalls = 200 };
            settings.Profiles.Add(new GeneratorSettings { Width = 4, Height = 4, PlopCount = 1, WallDensity = 0.2 });
            settings.Profiles.Add(Small(2));
            var result = BatchGenerator.Generate(settings);
            Assert.AreEqual(12, result.Levels.Count);
            Assert.AreEqual(0, result.Stats.ReplayFailures);
            var report = LevelVerifier.VerifyPack(result.Levels);
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.AreEqual(result.Stats.Candidates, result.Stats.Accepted + result.Stats.Rejected);
        }

        [Test]
        public void NearDuplicates_DetectOneWallDifferenceAfterRotation()
        {
            var original = LevelText.Parse("a", "R..r", ".#..", "B..b");
            var almost = LevelText.Parse("b", "R..r", ".#.#", "B..b");
            var different = LevelText.Parse("c", "R..r", "....", ".B.b");
            var registry = new DuplicateRegistry();
            Assert.IsTrue(registry.TryAdd(original));
            var rotated = LevelCanonicalizer.TransformLevel(almost, 5);
            Assert.IsFalse(registry.Contains(rotated));
            Assert.IsTrue(registry.ContainsNearDuplicate(rotated));
            Assert.IsFalse(registry.ContainsNearDuplicate(different));
        }

        [Test]
        public void ShippedLevelPack_IsValidAndVerified()
        {
            string path = FindPack();
            if (path == null) Assert.Fail("Production pack not found.");
            var json = System.IO.File.ReadAllText(path);
            var levels = LevelSerializer.PackFromJson(json);
            var report = LevelVerifier.VerifyProductionPack(levels);
            Assert.IsTrue(report.IsValid, report.ToString());

            var runtime = LevelSerializer.PackFromJson(json, includeDevData: false);
            Assert.AreEqual(ProductionCatalog.LevelCount, runtime.Count);
            for (int i = 0; i < runtime.Count; i++)
            {
                Assert.AreEqual(levels[i].Id, runtime[i].Id);
                Assert.AreEqual("Level " + (i + 1), runtime[i].Name);
                Assert.AreEqual(i < 10, !string.IsNullOrEmpty(runtime[i].Instruction), runtime[i].Id);
                Assert.IsNull(runtime[i].Metadata);
            }

            for (int recovery = 14; recovery < levels.Count; recovery += 5)
                Assert.Less(levels[recovery].Metadata.Difficulty.Score, levels[recovery - 1].Metadata.Difficulty.Score);
        }

        [Test]
        public void ProductionVerification_RejectsAValidBoardWithNoStoredReplay()
        {
            var board = LevelText.Parse("a", "R..r");
            var level = board.WithMetadata(null, LevelCanonicalizer.CanonicalId(board));
            Assert.IsTrue(LevelVerifier.Verify(level).IsValid);
            var report = LevelVerifier.VerifyProductionPack(new[] { level });
            Assert.IsFalse(report.IsValid);
            StringAssert.Contains("No stored solution", report.ToString());
        }

        /// <summary>The Phase 8 campaign (archived in Phase 10; it no longer ships).</summary>
        internal static string FindPack() => FindLevelFile("PLOP_Pack_001.json");

        /// <summary>Finds a level data file in Assets/Levels/Resources/Levels or Assets/Levels/Archive.</summary>
        internal static string FindLevelFile(string name)
        {
            var dir = new System.IO.DirectoryInfo(System.IO.Directory.GetCurrentDirectory());
            while (dir != null)
            {
                foreach (var sub in new[] { System.IO.Path.Combine("Assets", "Levels", "Resources", "Levels"), System.IO.Path.Combine("Assets", "Levels", "Archive") })
                {
                    var candidate = System.IO.Path.Combine(dir.FullName, sub, name);
                    if (System.IO.File.Exists(candidate)) return candidate;
                }
                dir = dir.Parent;
            }
            return null;
        }

        /// <summary>The project's Assets folder, or null outside the repository.</summary>
        internal static string FindAssets()
        {
            string pack = FindPack();
            if (pack == null) return null;
            var dir = new System.IO.DirectoryInfo(System.IO.Path.GetDirectoryName(pack));
            while (dir != null && dir.Name != "Assets") dir = dir.Parent;
            return dir?.FullName;
        }
    }
}
