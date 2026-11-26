using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Plop.Core;
using Plop.Meta;
using Plop.Presentation;
using Plop.Solver;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Plop.Tests.PlayMode
{
    /// <summary>Phase 10 in the running game: tiles, forced-movement playback, Lava recovery, limits and paid hints.</summary>
    public class Phase10GameplayTests
    {
        private GameObject _root;
        private PlopGameController _controller;

        private static LevelDefinition Level(string id, int fanPower, params string[] rows)
        {
            var l = LevelText.Parse(fanPower, id, rows);
            return l.WithOptimalMoves(BfsSolver.Solve(l).SolutionLength);
        }

        private void Create(List<LevelDefinition> levels, int start = 0)
        {
            _root = new GameObject("Phase10 test");
            _controller = _root.AddComponent<PlopGameController>();
            _controller.RememberProgress = false;
            _controller.UseLevels(levels, start);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root);
        }

        private IEnumerator Settle()
        {
            float until = Time.unscaledTime + 3f;
            while ((_controller.Board.IsTraveling || _controller.RecoveryPending) && Time.unscaledTime < until) yield return null;
            yield return null;
        }

        private Button FindButton(string label) =>
            _root.GetComponentsInChildren<Button>().Single(b => b.GetComponentInChildren<Text>()?.text == label);

        private string ModalTitle => _root.GetComponentsInChildren<Text>(true).Single(t => t.name == "Deadlock title").text;

        [Test]
        public void Board_DrawsEveryMechanic_WithShapeCues()
        {
            var scene = new GameObject("Tiles");
            try
            {
                var board = scene.AddComponent<BoardView>();
                var level = LevelText.Parse(2, "tiles", "1~..r", "R!>.1");
                board.Build(level, level.CreateInitialState(), ShapeAtlas.ForDensity(96f));
                var names = scene.GetComponentsInChildren<Transform>().Select(t => t.name).ToList();
                Assert.IsTrue(names.Any(n => n.StartsWith("Frost (1,1)")));
                Assert.IsTrue(names.Any(n => n.StartsWith("Lava (1,0)")));
                Assert.AreEqual(2, scene.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Loop ")));
                // Phase 11.6: the strength chevrons are the direction cue (one per cell of push, pointing downwind).
                var chevrons = scene.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Breeze power")).ToList();
                Assert.AreEqual(2, chevrons.Count, "one chevron per cell of push");
                foreach (var arrow in chevrons) Assert.Less(Vector3.Distance(Vector3.right, arrow.up), 1e-4f, "the chevron points the fan's way");
                var textures = scene.GetComponentsInChildren<SpriteRenderer>().Where(r => r.sprite != null).Select(r => r.sprite.texture).Distinct().Count();
                // Phase 11.5: surfaces, Loops and wind use one shared procedural quad; everything else the board atlas.
                Assert.LessOrEqual(textures, PlopShaders.Available ? 2 : 1, "mechanic tiles share the board atlas (and the shader quad)");
            }
            finally { Object.Destroy(scene); }
        }

        [UnityTest]
        public IEnumerator Lava_ShowsAFriendlyCard_AndUndoRecoversWithoutPenalty()
        {
            Create(new List<LevelDefinition> { Level("lava", 1, "R!r", "...") });
            yield return null;
            _controller.Wallet.Grant("test", 30);
            _controller.Move(Direction.Right);
            Assert.IsTrue(_controller.Engine.IsFailed);
            yield return Settle();
            var hud = _root.GetComponent<GameHud>();
            Assert.IsTrue(hud.IsDeadlockOpen);
            Assert.AreEqual("Oops! Too hot!", ModalTitle);
            _controller.Move(Direction.Down);
            Assert.AreEqual(1, _controller.Engine.MoveCount, "the card intercepts swipes");
            FindButton("Undo Move").onClick.Invoke();
            Assert.IsFalse(_controller.Engine.IsFailed);
            Assert.AreEqual(0, _controller.Engine.MoveCount);
            Assert.AreEqual(30, _controller.Wallet.Balance, "no Dream Dust is taken");
            Assert.AreEqual(_controller.Board.CellToWorld(new GridPosition(0, 1)), _controller.Board.PlopViewPosition(0));
        }

        [UnityTest]
        public IEnumerator Lava_RestartFromTheCard()
        {
            Create(new List<LevelDefinition> { Level("lava-restart", 1, "R~.!r", ".....") });
            yield return null;
            _controller.Move(Direction.Down);
            yield return Settle();
            _controller.Move(Direction.Up);
            yield return Settle();
            _controller.Move(Direction.Right); // slides into the Lava
            yield return Settle();
            Assert.IsTrue(_root.GetComponent<GameHud>().IsDeadlockOpen);
            FindButton("Restart Level").onClick.Invoke();
            Assert.AreEqual(0, _controller.Engine.MoveCount);
            Assert.IsFalse(_controller.Engine.IsFailed);
        }

        [UnityTest]
        public IEnumerator ForcedChain_PlaysToTheLogicalCell_AndHoldsTheInputGate()
        {
            Create(new List<LevelDefinition> { Level("chain", 1, "1....r", "R~1...") });
            yield return null;
            _controller.Move(Direction.Right);
            Assert.IsTrue(_controller.Engine.IsCompleted, "Frost → Loop → slide into the Nest is one swipe");
            Assert.IsTrue(_controller.Board.IsTraveling, "the chain is still being shown");
            yield return Settle();
            Assert.IsFalse(_controller.Board.IsTraveling);
            Assert.AreEqual(_controller.Board.CellToWorld(new GridPosition(5, 1)), _controller.Board.PlopViewPosition(0));
            Assert.IsTrue(_controller.Board.PlopViewAt(0).IsAsleep);
        }

        [UnityTest]
        public IEnumerator ReducedMotion_LandsTheWholeChainAtOnce()
        {
            Create(new List<LevelDefinition> { Level("chain-rm", 2, "....r", ".....", "R^...") });
            yield return null;
            _controller.SetReducedMotion(true);
            _controller.Move(Direction.Right);
            Assert.IsFalse(_controller.Board.IsTraveling);
            Assert.AreEqual(_controller.Board.CellToWorld(new GridPosition(1, 2)), _controller.Board.PlopViewPosition(0));
        }

        [UnityTest]
        public IEnumerator LimitedMoves_ShowsTheLimit_AndTheOutOfMovesCard()
        {
            Create(new List<LevelDefinition> { Level("limit", 1, "R...r").WithMoveLimit(2) });
            yield return null;
            var moves = _root.GetComponentsInChildren<Text>(true).Single(t => t.name == "Moves");
            _controller.Move(Direction.Right);
            yield return Settle();
            Assert.AreEqual("1 / 2", moves.text);
            _controller.Move(Direction.Right);
            yield return Settle();
            Assert.IsTrue(_controller.Engine.IsOutOfMoves);
            Assert.AreEqual("Out of moves!", ModalTitle);
            FindButton("Undo Move").onClick.Invoke();
            Assert.AreEqual(1, _controller.Engine.MovesRemaining);
        }

        [UnityTest]
        public IEnumerator PaidHint_ChargesOncePerBoard_AndNeverWithoutDust()
        {
            var levels = new List<LevelDefinition>();
            for (int i = 0; i < 8; i++) levels.Add(Level("paid" + i, 1, "R...r", "....."));
            Create(levels, PlopGameController.FreeHintLevels + 1);
            yield return null;
            var hud = _root.GetComponent<GameHud>();
            Assert.AreEqual("Hint " + RewardRules.HintCost, hud.HintLabel);

            var broke = _controller.RequestHintAsync();
            yield return new WaitUntil(() => broke.IsCompleted);
            Assert.IsFalse(broke.Result.HasValue, "no Dream Dust, no hint, no charge");
            Assert.IsFalse(_controller.HintUsedThisVisit);

            _controller.Wallet.Grant("test", 40);
            var first = _controller.RequestHintAsync();
            yield return new WaitUntil(() => first.IsCompleted);
            Assert.IsTrue(first.Result.HasValue);
            Assert.AreEqual(40 - RewardRules.HintCost, _controller.Wallet.Balance);
            StringAssert.Contains("−" + RewardRules.HintCost + " Dream Dust", hud.HintText, "the charge is shown");
            var again = _controller.RequestHintAsync();
            yield return new WaitUntil(() => again.IsCompleted);
            Assert.AreEqual(40 - RewardRules.HintCost, _controller.Wallet.Balance, "the same board's hint is never charged twice");

            _controller.Move(first.Result.Value);
            yield return Settle();
            var next = _controller.RequestHintAsync();
            yield return new WaitUntil(() => next.IsCompleted);
            Assert.AreEqual(40 - 2 * RewardRules.HintCost, _controller.Wallet.Balance, "a new board is a new hint");
        }

        [UnityTest]
        public IEnumerator IntroHints_AreFree()
        {
            Create(new List<LevelDefinition> { Level("free", 1, "R...r", ".....") });
            yield return null;
            Assert.AreEqual("Hint", _root.GetComponent<GameHud>().HintLabel);
            var hint = _controller.RequestHintAsync();
            yield return new WaitUntil(() => hint.IsCompleted);
            Assert.IsTrue(hint.Result.HasValue);
            Assert.AreEqual(0, _controller.Wallet.Balance);
        }

        [UnityTest]
        public IEnumerator BoardDecor_IsBoughtAtomically_AndOnlyTintsTheTray()
        {
            Create(new List<LevelDefinition> { Level("decor", 1, "R~..r", ".....") });
            yield return null;
            var tray = _root.GetComponentsInChildren<SpriteRenderer>(true).Single(r => r.name == "BoardPanel");
            Color before = tray.color;
            var cosmetics = _controller.Cosmetics;
            Assert.AreEqual(UnlockResult.InsufficientDust, cosmetics.TryUnlock("board.honey"));
            Assert.IsFalse(cosmetics.IsOwned("board.honey"));
            _controller.Wallet.Grant("test", 70);
            Assert.AreEqual(UnlockResult.Unlocked, cosmetics.TryUnlock("board.honey"));
            Assert.AreEqual(10, _controller.Wallet.Balance, "the price is taken exactly once");
            Assert.AreEqual(UnlockResult.AlreadyOwned, cosmetics.TryUnlock("board.honey"));
            Assert.IsTrue(cosmetics.Equip("board.honey"));
            var state = _controller.Engine.CurrentState.Clone();
            _controller.ApplyLook();
            Assert.AreEqual("honey", _controller.Look.Board.Name);
            Assert.AreNotEqual(before, tray.color, "the tray takes the decoration's tint");
            Assert.AreEqual(state, _controller.Engine.CurrentState, "decoration never touches the puzzle");
            Assert.IsNotNull(_root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("Frost")), "mechanic tiles keep their art");
        }

        [Test]
        public void GameplayScene_LoadsTheShippedPack()
        {
            Assert.IsNotNull(Resources.Load<TextAsset>(LevelLibrary.DefaultPack), "the shipped pack is in Resources");
            string scene = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "Scenes", "Gameplay.unity"));
            foreach (var line in scene.Split('\n'))
                if (line.Contains("levelPackResource:"))
                    StringAssert.EndsWith(LevelLibrary.DefaultPack, line.Trim(), "the device build must not load the archived pack");
            StringAssert.Contains("levelPackResource:", scene);
        }

        [Test]
        public void Migration_KeepsUnchangedPuzzlesAndAccess_ButNeverInventsCompletions()
        {
            var levels = new List<LevelDefinition>();
            for (int i = 0; i < 6; i++) levels.Add(LevelText.Parse("new" + i, "R" + new string('.', i + 1) + "r"));
            var progress = new LevelProgress(levels);
            const string previous = "{\"resumeId\":\"old9\",\"completedIds\":[\"new0\",\"old1\",\"old2\"],\"unlockedIds\":[\"new0\",\"old1\",\"old2\",\"old9\"]}";
            Assert.IsTrue(progress.MigrateFromPreviousCampaign(previous, -1));
            Assert.IsTrue(progress.IsCompleted(0), "an unchanged puzzle keeps its completion");
            Assert.IsFalse(progress.IsCompleted(1), "a replaced puzzle is never marked complete");
            Assert.IsTrue(progress.IsUnlocked(3), "three levels beaten → four slots open");
            Assert.IsFalse(progress.IsUnlocked(4));
            Assert.AreEqual(1, progress.ResumeIndex());
        }
    }
}
