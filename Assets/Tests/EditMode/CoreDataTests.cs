using System;
using NUnit.Framework;
using Plop.Core;

namespace Plop.Tests
{
    /// <summary>Validation, serialization and randomized invariants of the core simulation.</summary>
    public class CoreDataTests
    {
        // ---------------- validation ----------------

        [Test]
        public void InvalidLevel_PlopOutsideGrid_Rejected()
        {
            const string expected = "outside";
            var level = new LevelDefinition("x", "x", 3, 3, null,
                new[] { new PlopDefinition(0, PlopColor.Red, new GridPosition(5, 0)) },
                new[] { new NestDefinition(PlopColor.Red, new GridPosition(1, 1)) });
            var v = LevelValidator.Validate(level);
            Assert.IsFalse(v.IsValid);
            StringAssert.Contains(expected, v.ToString());
        }

        [Test]
        public void InvalidLevel_OverlappingPlops_Rejected()
        {
            var level = new LevelDefinition("x", "x", 3, 3, null,
                new[] { new PlopDefinition(0, PlopColor.Red, new GridPosition(0, 0)), new PlopDefinition(1, PlopColor.Blue, new GridPosition(0, 0)) },
                new[] { new NestDefinition(PlopColor.Red, new GridPosition(1, 1)), new NestDefinition(PlopColor.Blue, new GridPosition(2, 2)) });
            StringAssert.Contains("overlap", LevelValidator.Validate(level).ToString());
        }

        [Test]
        public void InvalidLevel_PlopOnWall_Rejected()
        {
            var level = new LevelDefinition("x", "x", 3, 3, new[] { new GridPosition(0, 0) },
                new[] { new PlopDefinition(0, PlopColor.Red, new GridPosition(0, 0)) },
                new[] { new NestDefinition(PlopColor.Red, new GridPosition(1, 1)) });
            StringAssert.Contains("wall", LevelValidator.Validate(level).ToString());
        }

        [Test]
        public void InvalidLevel_NestOnWall_Rejected()
        {
            var level = new LevelDefinition("x", "x", 3, 3, new[] { new GridPosition(1, 1) },
                new[] { new PlopDefinition(0, PlopColor.Red, new GridPosition(0, 0)) },
                new[] { new NestDefinition(PlopColor.Red, new GridPosition(1, 1)) });
            StringAssert.Contains("on a wall", LevelValidator.Validate(level).ToString());
        }

        [Test]
        public void InvalidLevel_MissingNest_Rejected()
        {
            var level = LevelText.Parse("x", "R.B", "r..");
            StringAssert.Contains("Blue", LevelValidator.Validate(level).ToString());
        }

        [Test]
        public void InvalidLevel_PlopStartsOnOwnNest_Rejected()
        {
            var level = new LevelDefinition("x", "x", 2, 1, null,
                new[] { new PlopDefinition(0, PlopColor.Red, new GridPosition(0, 0)) },
                new[] { new NestDefinition(PlopColor.Red, new GridPosition(0, 0)) });
            StringAssert.Contains("matching Nest", LevelValidator.Validate(level).ToString());
        }

        [Test]
        public void InvalidLevel_BadIds_EmptyId_NoPlops_Rejected()
        {
            var badIds = new LevelDefinition("x", "x", 3, 1, null,
                new[] { new PlopDefinition(3, PlopColor.Red, new GridPosition(0, 0)) },
                new[] { new NestDefinition(PlopColor.Red, new GridPosition(2, 0)) });
            Assert.IsFalse(LevelValidator.Validate(badIds).IsValid);
            var noId = LevelText.Parse("", "R.r");
            Assert.IsFalse(LevelValidator.Validate(noId).IsValid);
            var none = new LevelDefinition("x", "x", 3, 1, null, null, null);
            Assert.IsFalse(LevelValidator.Validate(none).IsValid);
        }

        [Test]
        public void Engine_RejectsInvalidLevel()
        {
            var engine = new GameEngine();
            Assert.Throws<InvalidLevelException>(() => engine.LoadLevel(LevelText.Parse("x", "R.B", "r..")));
        }

        [Test]
        public void Pack_DuplicateIds_Rejected()
        {
            var a = LevelText.Parse("same", "R.r");
            var b = LevelText.Parse("same", "B.b");
            StringAssert.Contains("Duplicate level id", LevelValidator.ValidatePack(new[] { a, b }).ToString());
        }

        [Test]
        public void ValidateState_DetectsInconsistentSleepFlag()
        {
            var level = LevelText.Parse("x", "R.r");
            var s = level.CreateInitialState();
            s.SetPlop(0, level.CellIndex(2, 0), false); // on its nest but marked awake
            Assert.IsFalse(LevelValidator.ValidateState(level, s).IsValid);
        }

        // ---------------- serialization ----------------

        [Test]
        public void LevelJson_RoundTrip_PreservesEverything()
        {
            var level = LevelText.Parse("lvl-1", "R.#.r", ".B..b", "#...#").WithMetadata(new LevelMetadata
            {
                GeneratorVersion = 1,
                Seed = 0xDEADBEEFCAFEUL,
                GeneratorParams = "w=5 h=3",
                SolverVersion = 1,
                Solution = "RRUD",
                Difficulty = new DifficultyInfo { Score = 12.5, Category = DifficultyCategory.Easy }
            });
            level.Metadata.Difficulty.Metrics["solutionLength"] = 4;
            string json = LevelSerializer.ToJson(level);
            var back = LevelSerializer.FromJson(json);
            Assert.AreEqual(json, LevelSerializer.ToJson(back));
            Assert.AreEqual(LevelText.Render(level), LevelText.Render(back));
            Assert.AreEqual(0xDEADBEEFCAFEUL, back.Metadata.Seed);
            Assert.AreEqual(DifficultyCategory.Easy, back.Metadata.Difficulty.Category);
            Assert.AreEqual("RRUD", back.Metadata.Solution);
        }

        [Test]
        public void LevelJson_RuntimeLoad_SkipsDevData()
        {
            var level = LevelText.Parse("lvl", "R.r").WithMetadata(new LevelMetadata { Solution = "RR" });
            var runtime = LevelSerializer.FromJson(LevelSerializer.ToJson(level), includeDevData: false);
            Assert.IsNull(runtime.Metadata);
            Assert.IsFalse(LevelSerializer.ToJson(level, includeDevData: false).Contains("\"dev\""));
        }

        [Test]
        public void LevelJson_InvalidData_Throws()
        {
            Assert.Throws<FormatException>(() => LevelSerializer.FromJson("{\"schemaVersion\": 99, \"id\":\"a\"}"));
            Assert.Throws<FormatException>(() => LevelSerializer.FromJson("{\"schemaVersion\": 1, \"id\":\"a\", \"width\":3}"));
            Assert.Throws<FormatException>(() => LevelSerializer.FromJson(
                "{\"schemaVersion\":1,\"id\":\"a\",\"width\":3,\"height\":1,\"walls\":[],\"plops\":[{\"id\":0,\"color\":\"Pink\",\"pos\":[0,0]}],\"nests\":[]}"));
            Assert.Throws<FormatException>(() => LevelSerializer.FromJson("{ not json"));
        }

        [Test]
        public void PackJson_RoundTrip()
        {
            var levels = new[] { LevelText.Parse("a", "R.r"), LevelText.Parse("b", "B..b") };
            var back = LevelSerializer.PackFromJson(LevelSerializer.PackToJson("pack", levels));
            Assert.AreEqual(2, back.Count);
            Assert.AreEqual("b", back[1].Id);
        }

        [Test]
        public void StateJson_RoundTrip()
        {
            var level = LevelText.Parse("st", "R..r", "B..b");
            var e = new GameEngine();
            e.LoadLevel(level);
            e.TryMove(Direction.Right);
            string json = LevelSerializer.StateToJson(level, e.CurrentState);
            var back = LevelSerializer.StateFromJson(level, json);
            Assert.AreEqual(e.CurrentState, back);
            var e2 = new GameEngine();
            e2.LoadState(level, back);
            e2.TryMove(Direction.Right);
            e.TryMove(Direction.Right);
            Assert.AreEqual(e.CurrentState, e2.CurrentState);
        }

        // ---------------- randomized invariants ----------------

        [Test]
        public void Randomized_Invariants_HoldForRandomLevelsAndMoves([Range(0, 9)] int batch)
        {
            var rng = new Random(1234 + batch);
            for (int trial = 0; trial < 40; trial++)
            {
                var level = RandomLevel(rng);
                if (!LevelValidator.Validate(level).IsValid) continue;
                var engine = new GameEngine();
                engine.LoadLevel(level);
                var initial = engine.GetState();
                int changes = 0;
                for (int step = 0; step < 30; step++)
                {
                    var before = engine.GetState();
                    var r = engine.TryMove((Direction)rng.Next(4));
                    var after = engine.CurrentState;
                    Assert.IsTrue(LevelValidator.ValidateState(level, after).IsValid, "invalid state " + after.ToString(level));
                    for (int id = 0; id < after.PlopCount; id++)
                    {
                        if (before.IsAsleep(id)) Assert.AreEqual(before.CellOf(id), after.CellOf(id), "sleeping plop moved");
                        int dx = Math.Abs(level.PositionOf(before.CellOf(id)).X - level.PositionOf(after.CellOf(id)).X);
                        int dy = Math.Abs(level.PositionOf(before.CellOf(id)).Y - level.PositionOf(after.CellOf(id)).Y);
                        Assert.LessOrEqual(dx + dy, 1, "plop moved more than one cell");
                    }
                    if (r.Status == MoveStatus.NoOp) Assert.AreEqual(before, after);
                    if (r.Status == MoveStatus.Moved) { changes++; Assert.AreNotEqual(before, after); }
                    Assert.AreEqual(changes, engine.MoveCount);
                    if (engine.IsCompleted) break;
                }
                while (engine.Undo()) { }
                Assert.AreEqual(initial, engine.CurrentState, "undo-all must restore the initial state");
            }
        }

        [Test]
        public void Randomized_PlopIdPermutation_DoesNotChangeOutcome([Range(0, 4)] int batch)
        {
            var rng = new Random(99 + batch);
            for (int trial = 0; trial < 40; trial++)
            {
                var level = RandomLevel(rng);
                if (!LevelValidator.Validate(level).IsValid) continue;
                // reverse plop ids
                int n = level.Plops.Count;
                var permuted = new PlopDefinition[n];
                for (int i = 0; i < n; i++)
                {
                    var p = level.Plops[i];
                    permuted[i] = new PlopDefinition(n - 1 - p.Id, p.Color, p.Start);
                }
                var level2 = new LevelDefinition("perm", "perm", level.Width, level.Height, level.Walls, permuted, level.Nests);
                var a = new GameEngine(); a.LoadLevel(level);
                var b = new GameEngine(); b.LoadLevel(level2);
                for (int step = 0; step < 25; step++)
                {
                    var d = (Direction)rng.Next(4);
                    a.TryMove(d); b.TryMove(d);
                    Assert.AreEqual(LevelText.Render(level, a.CurrentState), LevelText.Render(level2, b.CurrentState));
                }
            }
        }

        private static LevelDefinition RandomLevel(Random rng)
        {
            int w = rng.Next(3, 8), h = rng.Next(3, 8), n = rng.Next(1, 4);
            var cells = new int[w * h];
            for (int i = 0; i < cells.Length; i++) cells[i] = i;
            for (int i = cells.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); int t = cells[i]; cells[i] = cells[j]; cells[j] = t; }
            int k = 0;
            var plops = new PlopDefinition[n];
            var nests = new NestDefinition[n];
            for (int i = 0; i < n; i++) plops[i] = new PlopDefinition(i, (PlopColor)i, new GridPosition(cells[k] % w, cells[k++] / w));
            for (int i = 0; i < n; i++) nests[i] = new NestDefinition((PlopColor)i, new GridPosition(cells[k] % w, cells[k++] / w));
            int wallCount = rng.Next(0, Math.Max(1, (w * h) / 4));
            var walls = new GridPosition[wallCount];
            for (int i = 0; i < wallCount; i++) walls[i] = new GridPosition(cells[k] % w, cells[k++] / w);
            return new LevelDefinition("rnd", "rnd", w, h, walls, plops, nests);
        }
    }
}
