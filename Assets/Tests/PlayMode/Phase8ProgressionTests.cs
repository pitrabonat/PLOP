using System;
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
    /// <summary>Main menu, Perfect Moves, Dream Dust, Daily Gift, Daily Puzzle and save migration (Phase 8).</summary>
    public class Phase8ProgressionTests
    {
        private GameObject _root;
        private PlopGameController _controller;
        private FixedClock _clock;

        private static LevelDefinition Level(string id, params string[] rows)
        {
            var l = LevelText.Parse(id, rows);
            return l.WithOptimalMoves(BfsSolver.Solve(l).SolutionLength);
        }

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Phase8 test");
            _controller = _root.AddComponent<PlopGameController>();
            _controller.RememberProgress = false;
            _clock = new FixedClock(new DateTime(2026, 11, 24, 10, 0, 0));
            _controller.Clock = _clock;
            _controller.UseLevels(new List<LevelDefinition>
            {
                Level("one", "R..r"),
                Level("two", "R.r", "B.b"),
                Level("three", "R...r")
            });
            _controller.UseDailyPool(new List<LevelDefinition> { Level("dailyA", "B..b"), Level("dailyB", "Y...y") });
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.Destroy(_root);

        private IEnumerator Solve(IEnumerable<Direction> moves)
        {
            foreach (var d in moves)
            {
                _controller.Move(d);
                for (int i = 0; i < 60 && _controller.Board.IsTraveling; i++) yield return null;
            }
            yield return null;
        }

        private IEnumerator SolveOptimally()
        {
            var level = _controller.Engine.Level;
            yield return Solve(BfsSolver.Solve(level, _controller.Engine.CurrentState).Moves);
        }

        private string CompletionText(string name) =>
            _root.GetComponentsInChildren<Text>(true).Single(t => t.name == name).text;

        [UnityTest]
        public IEnumerator Menu_BlocksSwipes_AndPlayResumesTheFrontier()
        {
            yield return null;
            _controller.ShowMenu();
            Assert.IsTrue(_controller.Menu.IsOpen);
            _controller.Move(Direction.Right);
            Assert.AreEqual(0, _controller.Engine.MoveCount, "the board must not move under the menu");
            _controller.StartCampaign();
            Assert.IsFalse(_controller.Menu.IsOpen);
            Assert.AreEqual("one", _controller.Engine.Level.Id);
            yield return SolveOptimally();
            Assert.IsTrue(_controller.Engine.IsCompleted);
            _controller.ShowMenu();
            yield return null;
            _controller.StartCampaign();
            Assert.AreEqual("two", _controller.Engine.Level.Id, "PLAY resumes the newly unlocked level");
        }

        [UnityTest]
        public IEnumerator PerfectSolve_AwardsBadgeAndDustOnce()
        {
            yield return null;
            yield return SolveOptimally();
            Assert.IsTrue(_controller.Records.IsPerfect("one"));
            Assert.AreEqual(RewardRules.LevelComplete + RewardRules.LevelPerfect, _controller.Wallet.Balance);
            Assert.AreEqual("Perfect!", CompletionText("Completion message"));
            StringAssert.Contains("+" + (RewardRules.LevelComplete + RewardRules.LevelPerfect), CompletionText("Completion result"));
            var hud = _root.GetComponent<GameHud>();
            Assert.IsTrue(hud.PerfectLookup(0), "the level list badges a Perfect level");
            Assert.IsFalse(hud.PerfectLookup(1));

            _controller.Restart();
            yield return SolveOptimally();
            Assert.AreEqual(RewardRules.LevelComplete + RewardRules.LevelPerfect, _controller.Wallet.Balance, "replays never pay twice");
            StringAssert.DoesNotContain("+", CompletionText("Completion result"));
        }

        [UnityTest]
        public IEnumerator SlowSolve_ShowsTarget_ThenReplayCanImprove()
        {
            yield return null;
            yield return Solve(new[] { Direction.Right, Direction.Left, Direction.Right, Direction.Right, Direction.Right });
            Assert.IsTrue(_controller.Engine.IsCompleted);
            Assert.IsFalse(_controller.Records.IsPerfect("one"));
            Assert.AreEqual("Sweet Dreams!", CompletionText("Completion message"));
            StringAssert.Contains("Perfect: 3", CompletionText("Completion result"));
            Assert.IsTrue(_controller.Records.TryGet("one", out var slow));
            Assert.AreEqual(5, slow.Best);

            _controller.Restart();
            yield return SolveOptimally();
            Assert.IsTrue(_controller.Records.TryGet("one", out var better));
            Assert.AreEqual(3, better.Best, "replaying improves the stored best result");
            Assert.IsTrue(better.Perfect);
        }

        [UnityTest]
        public IEnumerator HintedSolve_IsNotPerfect()
        {
            yield return null;
            var hint = _controller.RequestHintAsync();
            yield return new WaitUntil(() => hint.IsCompleted);
            Assert.IsTrue(hint.Result.HasValue);
            Assert.IsTrue(_controller.HintUsedThisVisit);
            _controller.Restart(); // restarting does not hide the revealed route
            yield return SolveOptimally();
            Assert.IsFalse(_controller.Records.IsPerfect("one"));
            Assert.AreEqual(RewardRules.LevelComplete, _controller.Wallet.Balance);
        }

        [UnityTest]
        public IEnumerator DailyPuzzle_RewardsOncePerDay_AndReturnsToMenu()
        {
            yield return null;
            Assert.IsTrue(_controller.StartDaily());
            Assert.IsTrue(_controller.PlayingDaily);
            string todays = _controller.Engine.Level.Id;
            Assert.AreEqual("Daily Puzzle", _controller.Engine.Level.Name);
            yield return SolveOptimally();
            Assert.AreEqual(RewardRules.DailyPuzzle + RewardRules.DailyPuzzlePerfect, _controller.Wallet.Balance);
            Assert.IsFalse(_controller.IsLevelCompleted(0), "daily puzzles never alter campaign progress");
            _controller.NextLevel();
            Assert.IsTrue(_controller.Menu.IsOpen);
            Assert.IsFalse(_controller.PlayingDaily);
            Assert.AreEqual("one", _controller.Engine.Level.Id, "leaving the daily restores the campaign board");

            _controller.StartDaily();
            Assert.AreEqual(todays, _controller.Engine.Level.Id, "same date, same puzzle");
            yield return SolveOptimally();
            Assert.AreEqual(RewardRules.DailyPuzzle + RewardRules.DailyPuzzlePerfect, _controller.Wallet.Balance, "no repeated daily reward");

            _clock.LocalNow = _clock.LocalNow.AddDays(1);
            _controller.ShowMenu();
            _controller.StartDaily();
            yield return SolveOptimally();
            Assert.Greater(_controller.Wallet.Balance, RewardRules.DailyPuzzle + RewardRules.DailyPuzzlePerfect, "a new day has a new reward");
        }

        [UnityTest]
        public IEnumerator DailyGift_ClaimsOncePerLocalDay()
        {
            yield return null;
            _controller.ShowMenu();
            Assert.AreEqual(RewardRules.DailyGift, _controller.ClaimDailyGift());
            Assert.AreEqual(0, _controller.ClaimDailyGift());
            _clock.LocalNow = _clock.LocalNow.AddHours(13); // still the same calendar day
            Assert.AreEqual(0, _controller.ClaimDailyGift());
            _clock.LocalNow = new DateTime(2026, 11, 25, 0, 5, 0);
            Assert.AreEqual(RewardRules.DailyGift, _controller.ClaimDailyGift());
            Assert.AreEqual(2 * RewardRules.DailyGift, _controller.Wallet.Balance);
        }

        [Test]
        public void SaveMigration_MapsRebuiltSlots_WithoutLosingProgress()
        {
            var levels = new List<LevelDefinition> { Level("new0", "R.r"), Level("new1", "R..r"), Level("new2", "R...r") };
            var progress = new LevelProgress(levels);
            const string history = "{\"slots\":[{\"slot\":0,\"id\":\"new0\",\"previousIds\":[\"old0\"]}," +
                                   "{\"slot\":1,\"id\":\"new1\",\"previousIds\":[\"old1\"]},{\"slot\":2,\"id\":\"new2\",\"previousIds\":[]}]}";
            Assert.IsTrue(progress.MigrateForTests(history, new[] { "old0", "old1" }, new[] { "old0", "old1" }, "old1"));
            Assert.IsTrue(progress.IsCompleted(0));
            Assert.IsTrue(progress.IsCompleted(1));
            Assert.IsTrue(progress.IsUnlocked(2), "completing the rebuilt slot 2's predecessor unlocks it");
            Assert.AreEqual("new1", progress.ResumeId);
        }

        [Test]
        public void Layout_SmallBoardsUseMostOfTheWidth()
        {
            var r = BoardLayout.Compute(1080, 1920, new Rect(0, 0, 1080, 1920), 5, 5);
            Assert.GreaterOrEqual(r.BoardRectPixels.width, 1080 * 0.85f, "a 5×5 board should use most of the usable width");
            var tall = BoardLayout.Compute(1290, 2796, new Rect(0, 102, 1290, 2796 - 102 - 177), 6, 7);
            Assert.GreaterOrEqual(tall.BoardRectPixels.width, 1290 * 0.85f);
            Assert.LessOrEqual(tall.BoardRectPixels.yMax, tall.AvailablePixels.yMax + 0.5f);
        }
    }
}
