using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Plop.Core;
using Plop.Generation;
using Plop.Solver;

namespace Plop.Tests
{
    /// <summary>Phase 10: Frost, Lava, Breeze, Loops, their interactions, failure, move limits and tooling support.</summary>
    public class MechanicsTests
    {
        private static GameEngine Load(LevelDefinition level)
        {
            var e = new GameEngine();
            e.LoadLevel(level);
            return e;
        }

        private static GridPosition At(GameEngine e, int plop) => e.Level.PositionOf(e.CurrentState.CellOf(plop));

        // ------------------------------------------------------------------ Frost

        [Test]
        public void Frost_SlidesUntilAWall_AsOneMove()
        {
            var e = Load(LevelText.Parse("frost-wall", "R~...#", "r....."));
            var result = e.TryMove(Direction.Right);
            Assert.AreEqual(new GridPosition(4, 1), At(e, 0));
            Assert.AreEqual(1, e.MoveCount, "a whole slide is one swipe");
            Assert.IsTrue(result.Events.Any(ev => ev.Type == GameEventType.PlopSlid && ev.From.X == 1 && ev.To.X == 4));
        }

        [Test]
        public void Frost_StopsAtTheBoardEdge()
        {
            var e = Load(LevelText.Parse("frost-edge", "R~...", "r...."));
            e.TryMove(Direction.Right);
            Assert.AreEqual(new GridPosition(4, 1), At(e, 0));
        }

        [Test]
        public void Frost_StopsBeforeAPlop_ThatAlreadySteppedThisCommand()
        {
            var e = Load(LevelText.Parse("frost-plop", "R~..B.", "r....b"));
            e.TryMove(Direction.Right);
            Assert.AreEqual(new GridPosition(5, 1), At(e, 1), "the front Plop steps first");
            Assert.AreEqual(new GridPosition(4, 1), At(e, 0), "the slider stops right behind it");
        }

        [Test]
        public void Frost_SleepingPlopIsAStopper()
        {
            var e = Load(LevelText.Parse("frost-sleeper", "R~.Yy.", "r....."));
            e.TryMove(Direction.Right);
            Assert.IsTrue(e.CurrentState.IsAsleep(1));
            Assert.AreEqual(new GridPosition(3, 1), At(e, 0));
        }

        [Test]
        public void Frost_SettlesOnTheMatchingNest_AndCrossesOthers()
        {
            var e = Load(LevelText.Parse("frost-nest", "R~b.r.", "B....."));
            e.TryMove(Direction.Right);
            Assert.AreEqual(new GridPosition(4, 1), At(e, 0));
            Assert.IsTrue(e.CurrentState.IsAsleep(0), "slides past a non-matching Nest and sleeps on its own");
        }

        // ------------------------------------------------------------------ Lava

        [Test]
        public void Lava_StepFails_ThenUndoAndRestartRecover()
        {
            var e = Load(LevelText.Parse("lava-step", "R!.r", "...."));
            var result = e.TryMove(Direction.Right);
            Assert.IsTrue(e.IsFailed);
            Assert.AreEqual(FailureReason.Lava, e.CurrentState.Failure);
            Assert.AreEqual(GameEventType.LevelFailed, result.Events.Last().Type);
            Assert.IsTrue(result.Events.Any(ev => ev.Type == GameEventType.PlopBurned));
            Assert.AreEqual(MoveStatus.Ignored, e.TryMove(Direction.Down).Status, "a failed attempt accepts no moves");
            Assert.IsTrue(e.Undo());
            Assert.IsFalse(e.IsFailed);
            Assert.AreEqual(new GridPosition(0, 1), At(e, 0));
            e.TryMove(Direction.Right);
            e.Restart();
            Assert.IsFalse(e.IsFailed);
            Assert.AreEqual(0, e.MoveCount);
        }

        [Test]
        public void Lava_SlideIntoLavaFails()
        {
            var e = Load(LevelText.Parse("lava-slide", "R~.!r", "....."));
            e.TryMove(Direction.Right);
            Assert.IsTrue(e.IsFailed);
            Assert.AreEqual(new GridPosition(3, 1), At(e, 0));
        }

        [Test]
        public void Lava_TwoHazardsInOneCommand_FirstInOrderFailsAndResolutionStops()
        {
            var e = Load(LevelText.Parse("lava-two", "R!..r", "B!..b"));
            e.TryMove(Direction.Right);
            Assert.IsTrue(e.IsFailed);
            Assert.AreEqual(1, e.CurrentState.FailedPlop, "Right: equal x, lower row first");
            Assert.AreEqual(new GridPosition(0, 1), At(e, 0), "nothing resolves after a failure");
        }

        [Test]
        public void Lava_FanPushIntoLavaFails()
        {
            var e = Load(LevelText.Parse(2, "fan-lava", "..r..", ".!...", "R^..."));
            e.TryMove(Direction.Right);
            Assert.IsTrue(e.IsFailed);
            Assert.AreEqual(new GridPosition(1, 1), At(e, 0));
        }

        // ------------------------------------------------------------------ Breeze

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Breeze_PushesUpToItsPower_IndependentOfTheSwipe(int power)
        {
            var e = Load(LevelText.Parse(power, "fan-power", "....r", ".....", ".....", ".....", "R^..."));
            var result = e.TryMove(Direction.Right);
            Assert.AreEqual(new GridPosition(1, power), At(e, 0));
            Assert.IsTrue(result.Events.Any(ev => ev.Type == GameEventType.PlopPushed && ev.Direction == Direction.Up));
        }

        [Test]
        public void Breeze_PushStopsBeforeAWall()
        {
            var e = Load(LevelText.Parse(3, "fan-wall", "....r", ".....", ".#...", ".....", "R^..."));
            e.TryMove(Direction.Right);
            Assert.AreEqual(new GridPosition(1, 1), At(e, 0));
        }

        [Test]
        public void Breeze_PushOntoFrost_BecomesALongSlide()
        {
            var e = Load(LevelText.Parse(1, "fan-frost", "....r", ".....", ".....", ".~...", "R^..."));
            e.TryMove(Direction.Right);
            Assert.AreEqual(new GridPosition(1, 4), At(e, 0));
        }

        [Test]
        public void Breeze_RedirectsASlide_WhichKeepsSliding()
        {
            var e = Load(LevelText.Parse(1, "slide-fan", "r....", ".....", ".....", ".....", "R~.^."));
            e.TryMove(Direction.Right);
            Assert.AreEqual(new GridPosition(3, 4), At(e, 0));
        }

        [Test]
        public void Breeze_FacingFans_ActOncePerCommand_AndTerminate()
        {
            var e = Load(LevelText.Parse(3, "fan-loop", "R><.r", "....."));
            e.TryMove(Direction.Right);
            Assert.AreEqual(new GridPosition(0, 1), At(e, 0), "pushed right, back left past the used fan, stopped by the edge");
            Assert.AreEqual(1, e.MoveCount);
        }

        // ------------------------------------------------------------------ Loops

        [Test]
        public void Loop_TeleportsToItsPair_AndAStepStopsThere()
        {
            var e = Load(LevelText.Parse("loop", "r...1", "R1..."));
            var result = e.TryMove(Direction.Right);
            Assert.AreEqual(new GridPosition(4, 1), At(e, 0));
            Assert.IsTrue(result.Events.Any(ev => ev.Type == GameEventType.PlopTeleported));
            Assert.IsTrue(e.Undo());
            Assert.AreEqual(new GridPosition(0, 0), At(e, 0), "Undo restores the pre-teleport cell");
        }

        [Test]
        public void Loop_OccupiedExitCancelsTheTeleport()
        {
            var e = Load(LevelText.Parse("loop-busy", "r.b.B1", "R1...."));
            var result = e.TryMove(Direction.Right);
            Assert.AreEqual(new GridPosition(5, 1), At(e, 0), "B (id 0, top row) stepped onto the far Loop first");
            Assert.AreEqual(new GridPosition(1, 0), At(e, 1), "both stay on their entry Loops");
            Assert.AreEqual(2, result.Events.Count(ev => ev.Type == GameEventType.PlopBlocked && ev.BlockReason == BlockReason.PortalExit));
        }

        [Test]
        public void Loop_PreservesDirection_SoASlideContinues()
        {
            var e = Load(LevelText.Parse("loop-frost", "1....r", "R~1..."));
            e.TryMove(Direction.Right);
            Assert.AreEqual(new GridPosition(5, 1), At(e, 0));
            Assert.IsTrue(e.IsCompleted, "Frost → Loop → slide into the Nest, in one swipe");
        }

        [Test]
        public void Loop_OnlyOneTeleportPerCommand()
        {
            // Sliding down out of the exit crosses the entry Loop again: it acts as floor for the rest of the command.
            var e = Load(LevelText.Parse("loop-once", "r.1", "..R", "..~", "..1", "..."));
            var result = e.TryMove(Direction.Down);
            Assert.AreEqual(new GridPosition(2, 0), At(e, 0));
            Assert.AreEqual(1, result.Events.Count(ev => ev.Type == GameEventType.PlopTeleported));
        }

        // ------------------------------------------------------------------ move limits

        [Test]
        public void MoveLimit_EndsTheAttempt_AndUndoRestoresTheCounter()
        {
            var e = Load(LevelText.Parse("limit", "R..r").WithMoveLimit(2));
            e.TryMove(Direction.Right);
            Assert.AreEqual(1, e.MovesRemaining);
            var last = e.TryMove(Direction.Right);
            Assert.IsTrue(e.IsOutOfMoves);
            Assert.AreEqual(GameEventType.OutOfMoves, last.Events.Last().Type);
            Assert.AreEqual(MoveStatus.Ignored, e.TryMove(Direction.Right).Status);
            e.Undo();
            Assert.IsFalse(e.IsOutOfMoves);
            Assert.AreEqual(1, e.MovesRemaining);
        }

        [Test]
        public void MoveLimit_ForcedMovementCostsNoExtraMoves()
        {
            var e = Load(LevelText.Parse("limit-slide", "R~...r").WithMoveLimit(1));
            e.TryMove(Direction.Right);
            Assert.IsTrue(e.IsCompleted);
            Assert.IsFalse(e.IsOutOfMoves);
        }

        // ------------------------------------------------------------------ solver, analyzer, tools

        [Test]
        public void Solver_AvoidsLava_AndCountsSwipesNotCells()
        {
            var level = LevelText.Parse("detour", "R!r", "...");
            var solved = BfsSolver.Solve(level);
            Assert.AreEqual(SolverStatus.Solved, solved.Status);
            Assert.AreEqual(4, solved.SolutionLength, "down, right, right, up: never through Lava");
            Assert.IsTrue(BfsSolver.VerifySolution(level, solved.Moves));

            var slide = LevelText.Parse("long-slide", "R~....r");
            Assert.AreEqual(1, BfsSolver.Solve(slide).SolutionLength, "six cells of sliding are one move");
        }

        [Test]
        public void Analyzer_HasOneFailureSink_ThatIsNeverAState()
        {
            var level = LevelText.Parse("sink", "R!r", "...");
            var a = StateSpaceAnalyzer.AnalyzeWithGraph(level);
            Assert.IsTrue(a.Complete);
            Assert.Greater(a.FailingMoves, 0);
            Assert.GreaterOrEqual(a.Graph.FailIndex, 0);
            Assert.AreEqual(a.Graph.Count - 1, a.ReachableStates, "the sink is not a board state");
            Assert.AreEqual(4, a.StartDistance);
            var insight = PuzzleInsight.Analyze(level, a);
            Assert.Greater(insight.HazardAlternatives, 0, "the route passes swipes that would burn");
        }

        [Test]
        public void Insight_CountsMechanicSteps()
        {
            var level = LevelText.Parse(1, "kinds", "1....r", "R~1...", "..^...");
            var a = StateSpaceAnalyzer.AnalyzeWithGraph(level);
            Assert.IsTrue(a.Solvable);
            var insight = PuzzleInsight.Analyze(level, a);
            Assert.Greater(insight.MechanicSteps, 0);
            Assert.GreaterOrEqual(insight.MechanicKindsUsed, 2);
        }

        [Test]
        public void Validator_RejectsBrokenMechanics()
        {
            Assert.IsFalse(LevelValidator.Validate(LevelText.Parse("one-loop", "R1.r")).IsValid, "a Loop needs its pair");
            Assert.IsFalse(LevelValidator.Validate(LevelText.Parse(4, "weak", "R>.r")).IsValid, "fan power 1–3");
            var onNest = LevelText.Parse("nest-tile", "R..r").WithTiles(new[] { MechanicTile.Frost(3, 0) });
            Assert.IsFalse(LevelValidator.Validate(onNest).IsValid, "no Nests on mechanic tiles");
            var underPlop = LevelText.Parse("plop-tile", "R..r").WithTiles(new[] { MechanicTile.Lava(0, 0) });
            Assert.IsFalse(LevelValidator.Validate(underPlop).IsValid, "Plops start on plain floor");
        }

        [Test]
        public void Serialization_RoundTripsTilesAndLimits_AndKeepsClassicSchema()
        {
            var level = LevelText.Parse(2, "json", "1~..r", "R!^.1").WithMoveLimit(6);
            string json = LevelSerializer.ToJson(level);
            StringAssert.Contains("\"schemaVersion\": 2", json);
            var back = LevelSerializer.FromJson(json);
            CollectionAssert.AreEquivalent(level.Tiles.ToArray(), back.Tiles.ToArray());
            Assert.AreEqual(6, back.MoveLimit);
            StringAssert.Contains("\"schemaVersion\": 1", LevelSerializer.ToJson(LevelText.Parse("classic", "R.r")));
            Assert.Throws<FormatException>(() => LevelSerializer.FromJson(json.Replace("\"Fan\"", "\"Volcano\"")));
        }

        [Test]
        public void CanonicalId_FollowsMechanicsThroughSymmetry_AndKeepsClassicIds()
        {
            var level = LevelText.Parse(2, "sym", "1~..r", "R!^.1", "....#");
            string id = LevelCanonicalizer.CanonicalId(level);
            int optimal = BfsSolver.Solve(level).SolutionLength;
            for (int t = 1; t < 8; t++)
            {
                var turned = LevelCanonicalizer.TransformLevel(level, t);
                Assert.AreEqual(id, LevelCanonicalizer.CanonicalId(turned), "symmetry " + t);
                Assert.AreEqual(optimal, BfsSolver.Solve(turned).SolutionLength, "rules are symmetric, symmetry " + t);
            }
            Assert.AreNotEqual(id, LevelCanonicalizer.CanonicalId(level.WithTiles(Array.Empty<MechanicTile>())));
            Assert.AreNotEqual(id, LevelCanonicalizer.CanonicalId(level.WithMoveLimit(9)), "a challenge limit is a different puzzle");
        }

        [Test]
        public void StateCodec_StoresOnlyLiveStates()
        {
            var level = LevelText.Parse("codec", "R!r", "...");
            var e = Load(level);
            e.TryMove(Direction.Right);
            Assert.IsTrue(e.CurrentState.Failed);
            var codec = new StateCodec(level);
            var decoded = codec.Decode(codec.Encode(e.CurrentState));
            Assert.IsFalse(decoded.Failed);
            Assert.AreNotEqual(e.CurrentState, decoded, "failure is part of state equality");
        }

        // ------------------------------------------------------------------ determinism & termination fuzz

        [Test]
        public void RandomMechanicBoards_AreDeterministic_Bounded_AndOverlapFree()
        {
            var rng = new SplitMix64(0xF00DUL);
            int checkedMoves = 0;
            for (int trial = 0; trial < 300; trial++)
            {
                var level = RandomMechanicLevel(rng, trial);
                if (!LevelValidator.Validate(level).IsValid) continue;
                var a = Load(level);
                var b = Load(level);
                for (int m = 0; m < 25 && a.CanPlay; m++)
                {
                    var d = (Direction)rng.NextInt(4);
                    var ra = a.TryMove(d);
                    var rb = b.TryMove(d);
                    Assert.AreEqual(a.CurrentState, b.CurrentState, "same command, same result");
                    CollectionAssert.AreEqual(ra.Events.Select(x => x.ToString()).ToArray(), rb.Events.Select(x => x.ToString()).ToArray());
                    var cells = new HashSet<int>();
                    for (int id = 0; id < a.CurrentState.PlopCount; id++)
                    {
                        int cell = a.CurrentState.CellOf(id);
                        Assert.IsTrue(cells.Add(cell), "no two Plops share a cell");
                        Assert.IsFalse(level.IsWallCell(cell));
                    }
                    checkedMoves++;
                }
            }
            Assert.Greater(checkedMoves, 1000);
        }

        internal static LevelDefinition RandomMechanicLevel(SplitMix64 rng, int index)
        {
            int w = 4 + rng.NextInt(3), h = 4 + rng.NextInt(3);
            var used = new HashSet<int>();
            GridPosition Free()
            {
                while (true)
                {
                    var p = new GridPosition(rng.NextInt(w), rng.NextInt(h));
                    if (used.Add(p.Y * w + p.X)) return p;
                }
            }
            var walls = new List<GridPosition>();
            for (int i = 0; i < 2; i++) walls.Add(Free());
            var plops = new List<PlopDefinition>();
            var nests = new List<NestDefinition>();
            int count = 1 + rng.NextInt(3);
            for (int i = 0; i < count; i++)
            {
                plops.Add(new PlopDefinition(i, (PlopColor)i, Free()));
                nests.Add(new NestDefinition((PlopColor)i, Free()));
            }
            var tiles = new List<MechanicTile>();
            for (int i = 0; i < 3; i++) { var p = Free(); tiles.Add(MechanicTile.Frost(p.X, p.Y)); }
            for (int i = 0; i < 2; i++) { var p = Free(); tiles.Add(MechanicTile.Fan(p.X, p.Y, (Direction)rng.NextInt(4), 1 + rng.NextInt(3))); }
            { var p = Free(); tiles.Add(MechanicTile.Lava(p.X, p.Y)); }
            { var p = Free(); var q = Free(); tiles.Add(MechanicTile.Portal(p.X, p.Y, 0)); tiles.Add(MechanicTile.Portal(q.X, q.Y, 0)); }
            return new LevelDefinition("fuzz-" + index, "fuzz", w, h, walls, plops, nests, tiles: tiles);
        }
    }
}

namespace Plop.Tests
{
    /// <summary>Phase 10 economy: Dream Dust hint purchases.</summary>
    public class HintEconomyTests
    {
        [NUnit.Framework.Test]
        public void HintSpend_ChargesOncePerState_AndSurvivesRelaunch()
        {
            var store = new Plop.Meta.MemoryStore();
            var wallet = new Plop.Meta.DreamDustWallet(store);
            wallet.Grant("test", 40);
            string key = Plop.Meta.RewardRules.HintKey("PLOP-X", 0xABCDUL);
            NUnit.Framework.Assert.AreEqual(Plop.Meta.DreamDustWallet.SpendResult.Charged, wallet.TrySpend(key, Plop.Meta.RewardRules.HintCost));
            NUnit.Framework.Assert.AreEqual(Plop.Meta.DreamDustWallet.SpendResult.AlreadyPaid, wallet.TrySpend(key, Plop.Meta.RewardRules.HintCost));
            NUnit.Framework.Assert.AreEqual(40 - Plop.Meta.RewardRules.HintCost, wallet.Balance);
            var reloaded = new Plop.Meta.DreamDustWallet(store);
            NUnit.Framework.Assert.IsTrue(reloaded.HasSpent(key), "re-showing the same hint after a restart stays free");
            NUnit.Framework.Assert.AreEqual(Plop.Meta.DreamDustWallet.SpendResult.AlreadyPaid, reloaded.TrySpend(key, Plop.Meta.RewardRules.HintCost));
            NUnit.Framework.Assert.AreEqual(40 - Plop.Meta.RewardRules.HintCost, reloaded.Balance);
        }

        [NUnit.Framework.Test]
        public void HintSpend_InsufficientDustChargesNothing()
        {
            var wallet = new Plop.Meta.DreamDustWallet(new Plop.Meta.MemoryStore());
            wallet.Grant("test", Plop.Meta.RewardRules.HintCost - 1);
            NUnit.Framework.Assert.AreEqual(Plop.Meta.DreamDustWallet.SpendResult.InsufficientDust, wallet.TrySpend("hint:a:1", Plop.Meta.RewardRules.HintCost));
            NUnit.Framework.Assert.AreEqual(Plop.Meta.RewardRules.HintCost - 1, wallet.Balance);
            NUnit.Framework.Assert.IsFalse(wallet.HasSpent("hint:a:1"));
        }
    }
}
