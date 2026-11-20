using System.Collections.Generic;
using NUnit.Framework;
using Plop.Core;

namespace Plop.Tests
{
    /// <summary>Movement / collision / nest / undo / restart rules of the core simulation.</summary>
    public class CoreRulesTests
    {
        private static GameEngine Load(params string[] rows)
        {
            var engine = new GameEngine();
            engine.LoadLevel(LevelText.Parse("test", rows));
            return engine;
        }

        private static string Board(GameEngine e) => LevelText.Render(e.Level, e.CurrentState);

        private static string Rows(params string[] rows) => string.Join("\n", rows);

        [Test]
        public void SinglePlop_MovesOneCell()
        {
            var e = Load("....",
                         "R..r");
            var result = e.TryMove(Direction.Right);
            Assert.AreEqual(MoveStatus.Moved, result.Status);
            Assert.AreEqual(1, e.MoveCount);
            Assert.AreEqual(Rows("....", ".R.r"), Board(e));
        }

        [Test]
        public void Coordinates_UpIncreasesY_OriginBottomLeft()
        {
            var e = Load("...",
                         "...",
                         "R.r");
            Assert.AreEqual(new GridPosition(0, 0), e.Level.Plops[0].Start);
            e.TryMove(Direction.Up);
            Assert.AreEqual(new GridPosition(0, 1), e.Level.PositionOf(e.CurrentState.CellOf(0)));
        }

        [Test]
        public void MultiplePlops_AllMoveSimultaneously()
        {
            var e = Load("R...b",
                         "B...r");
            e.TryMove(Direction.Right);
            Assert.AreEqual(Rows(".R..b", ".B..r"), Board(e));
        }

        [Test]
        public void Wall_BlocksMovement_AndIsNoOp()
        {
            var e = Load("R#.r");
            var result = e.TryMove(Direction.Right);
            Assert.AreEqual(MoveStatus.NoOp, result.Status);
            Assert.AreEqual(0, e.MoveCount);
            Assert.IsFalse(e.CanUndo);
            Assert.AreEqual(1, result.Events.Length);
            Assert.AreEqual(GameEventType.PlopBlocked, result.Events[0].Type);
            Assert.AreEqual(BlockReason.Wall, result.Events[0].BlockReason);
        }

        [Test]
        public void GridEdge_BlocksMovement()
        {
            var e = Load("R..r");
            var result = e.TryMove(Direction.Left);
            Assert.AreEqual(MoveStatus.NoOp, result.Status);
            Assert.AreEqual(BlockReason.Edge, result.Events[0].BlockReason);
        }

        [Test]
        public void PartialBlock_OthersStillMove_CountsAsMove()
        {
            var e = Load("R#..r",
                         "B...b");
            var result = e.TryMove(Direction.Right);
            Assert.AreEqual(MoveStatus.Moved, result.Status);
            Assert.AreEqual(Rows("R#..r", ".B..b"), Board(e));
            Assert.AreEqual(1, e.MoveCount);
        }

        [Test]
        public void Ordering_FrontPlopMovesFirst_BackPlopFollows()
        {
            // Moving right: the rightmost Plop (B) moves first, so R can follow into the vacated cell.
            var e = Load("RB..rb");
            e.TryMove(Direction.Right);
            Assert.AreEqual(".RB.rb", Board(e));
        }

        [Test]
        public void Ordering_BlockedFrontPlop_BlocksPlopBehind()
        {
            var e = Load("RB#.rb");
            var result = e.TryMove(Direction.Right);
            Assert.AreEqual(MoveStatus.NoOp, result.Status);
            Assert.AreEqual("RB#.rb", Board(e));
            // R is blocked by Plop B
            bool sawPlopBlock = false;
            foreach (var ev in result.Events)
                if (ev.Type == GameEventType.PlopBlocked && ev.PlopId == 0 && ev.BlockReason == BlockReason.Plop && ev.BlockerId == 1) sawPlopBlock = true;
            Assert.IsTrue(sawPlopBlock);
        }

        [Test]
        public void Ordering_UpMovesHigherYFirst()
        {
            var e = Load("..",
                         "Rb",
                         "Br",
                         "..");
            // column 0: R at y=2 above B at y=1. Moving up: R first, then B follows.
            e.TryMove(Direction.Up);
            Assert.AreEqual(Rows("R.", "Bb", ".r", ".."), Board(e));
        }

        [Test]
        public void Ordering_HeadOn_PlopsDoNotSwap()
        {
            // Adjacent Plops never pass through each other: moving left, R (smaller x) moves first to x=0,
            // then B follows into x=1.
            var e = Load(".RB.rb");
            e.TryMove(Direction.Left);
            Assert.AreEqual("RB..rb", Board(e));
        }

        [Test]
        public void Ordering_IsIndependentOfPlopIds()
        {
            // Same layout, Plops listed in opposite id orders, must give identical boards after any sequence.
            var a = new LevelDefinition("a", "a", 5, 1, null,
                new[] { new PlopDefinition(0, PlopColor.Red, new GridPosition(0, 0)), new PlopDefinition(1, PlopColor.Blue, new GridPosition(1, 0)) },
                new[] { new NestDefinition(PlopColor.Red, new GridPosition(3, 0)), new NestDefinition(PlopColor.Blue, new GridPosition(4, 0)) });
            var b = new LevelDefinition("b", "b", 5, 1, null,
                new[] { new PlopDefinition(0, PlopColor.Blue, new GridPosition(1, 0)), new PlopDefinition(1, PlopColor.Red, new GridPosition(0, 0)) },
                new[] { new NestDefinition(PlopColor.Red, new GridPosition(3, 0)), new NestDefinition(PlopColor.Blue, new GridPosition(4, 0)) });
            var ea = new GameEngine(); ea.LoadLevel(a);
            var eb = new GameEngine(); eb.LoadLevel(b);
            foreach (var d in new[] { Direction.Right, Direction.Right, Direction.Left, Direction.Right, Direction.Right })
            {
                ea.TryMove(d); eb.TryMove(d);
                Assert.AreEqual(Board(ea), Board(eb));
            }
        }

        [Test]
        public void MatchingNest_PutsPlopToSleep_AndCompletes()
        {
            var e = Load("R.r");
            e.TryMove(Direction.Right);
            var result = e.TryMove(Direction.Right);
            Assert.IsTrue(result.CompletedLevel);
            Assert.IsTrue(e.IsCompleted);
            Assert.IsTrue(e.CurrentState.IsAsleep(0));
            Assert.AreEqual("..*", Board(e));
            var types = new List<GameEventType>();
            foreach (var ev in result.Events) types.Add(ev.Type);
            CollectionAssert.AreEqual(new[] { GameEventType.PlopMoved, GameEventType.PlopSettled, GameEventType.LevelCompleted }, types);
        }

        [Test]
        public void NonMatchingNest_IsTraversable()
        {
            var e = Load("Rb.r",
                         "B...");
            e.TryMove(Direction.Right); // R steps onto the blue nest and stays awake
            Assert.IsFalse(e.CurrentState.IsAsleep(0));
            Assert.AreEqual(e.Level.CellIndex(1, 1), e.CurrentState.CellOf(0));
            e.TryMove(Direction.Right); // and can leave it again
            Assert.AreEqual(e.Level.CellIndex(2, 1), e.CurrentState.CellOf(0));
        }

        [Test]
        public void SleepingPlop_NeverMoves_AndBlocksOthers()
        {
            var e = Load("Rr.b",
                         "...B");
            e.TryMove(Direction.Right); // R sleeps on its nest at (1,1); B blocked by edge
            Assert.IsTrue(e.CurrentState.IsAsleep(0));
            e.TryMove(Direction.Up);    // B moves up to (3,1)? (3,1) is b nest -> B sleeps
            Assert.IsTrue(e.IsCompleted);

            var e2 = Load("r...",
                          "R...",
                          "B.b.");
            e2.TryMove(Direction.Up); // R sleeps at (0,2); B follows to (0,1)
            Assert.IsTrue(e2.CurrentState.IsAsleep(0));
            var before = e2.GetState();
            var r = e2.TryMove(Direction.Up); // B blocked by sleeping R; R does not move
            Assert.AreEqual(MoveStatus.NoOp, r.Status);
            Assert.AreEqual(before, e2.CurrentState);
            Assert.AreEqual(BlockReason.Plop, r.Events[0].BlockReason);
            Assert.AreEqual(0, r.Events[0].BlockerId);
        }

        [Test]
        public void CompletedLevel_IgnoresFurtherMoves()
        {
            var e = Load("Rr");
            e.TryMove(Direction.Right);
            Assert.IsTrue(e.IsCompleted);
            Assert.AreEqual(MoveStatus.Ignored, e.TryMove(Direction.Left).Status);
            Assert.AreEqual(1, e.MoveCount);
        }

        [Test]
        public void Undo_RestoresExactPreviousState()
        {
            var e = Load("R..#",
                         "B..r",
                         "...b");
            var states = new List<GameState> { e.GetState() };
            foreach (var d in new[] { Direction.Right, Direction.Down, Direction.Right, Direction.Up })
            {
                if (e.TryMove(d).Changed) states.Add(e.GetState());
            }
            for (int i = states.Count - 1; i > 0; i--)
            {
                Assert.AreEqual(states[i], e.CurrentState);
                Assert.IsTrue(e.Undo());
                Assert.AreEqual(states[i - 1], e.CurrentState);
                Assert.AreEqual(i - 1, e.MoveCount);
            }
            Assert.IsFalse(e.Undo());
        }

        [Test]
        public void Undo_AfterCompletion_Works()
        {
            var e = Load("Rr");
            e.TryMove(Direction.Right);
            Assert.IsTrue(e.Undo());
            Assert.IsFalse(e.IsCompleted);
            Assert.IsFalse(e.CurrentState.IsAsleep(0));
        }

        [Test]
        public void Restart_RestoresInitialState_AndClearsHistory()
        {
            var e = Load("R..r",
                         "B..b");
            var initial = e.GetState();
            e.TryMove(Direction.Right);
            e.TryMove(Direction.Right);
            e.Restart();
            Assert.AreEqual(initial, e.CurrentState);
            Assert.AreEqual(0, e.MoveCount);
            Assert.IsFalse(e.CanUndo);
        }

        [Test]
        public void Events_AreRaisedForPresentation()
        {
            var e = new GameEngine();
            var seen = new List<GameEventType>();
            e.EventRaised += ev => seen.Add(ev.Type);
            e.LoadLevel(LevelText.Parse("t", "R.r"));
            e.TryMove(Direction.Right);
            e.Undo();
            e.Restart();
            CollectionAssert.AreEqual(new[] { GameEventType.LevelLoaded, GameEventType.PlopMoved, GameEventType.MoveUndone, GameEventType.LevelRestarted }, seen);
        }

        [Test]
        public void Simulation_IsDeterministic()
        {
            var level = LevelText.Parse("d", "R.#.r", ".B..b", "Y...y");
            var moves = new[] { Direction.Right, Direction.Up, Direction.Right, Direction.Down, Direction.Right, Direction.Right };
            string run1 = Run(level, moves), run2 = Run(level, moves);
            Assert.AreEqual(run1, run2);
        }

        private static string Run(LevelDefinition level, Direction[] moves)
        {
            var e = new GameEngine();
            e.LoadLevel(level);
            var log = new System.Text.StringBuilder();
            e.EventRaised += ev => log.Append(ev).Append('|');
            foreach (var m in moves) e.TryMove(m);
            return log + Board(e);
        }

        [Test]
        public void StateEquality_AndClone()
        {
            var level = LevelText.Parse("s", "R.r", "B.b");
            var a = level.CreateInitialState();
            var b = a.Clone();
            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            MoveRules.Apply(level, b, Direction.Right);
            Assert.AreNotEqual(a, b);
            a.CopyFrom(b);
            Assert.AreEqual(a, b);
        }

        [Test]
        public void StateCodec_RoundTrips_AndCanonicalizesSameColorPlops()
        {
            var level = LevelText.Parse("c", "R..R", "r..r");
            var codec = new StateCodec(level);
            var s = level.CreateInitialState();
            ulong key = codec.Encode(s);
            Assert.AreEqual(s, codec.Decode(key));
            // swap the two red plops: equivalent state, same canonical key
            var swapped = new GameState(2);
            swapped.SetPlop(0, s.CellOf(1), false);
            swapped.SetPlop(1, s.CellOf(0), false);
            Assert.AreEqual(key, codec.Encode(swapped));
        }
    }
}
