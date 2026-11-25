using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Plop.Core;
using Plop.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Plop.Tests.PlayMode
{
    /// <summary>Integration tests of the Unity prototype: controller + engine + board view + HUD.</summary>
    public class GameplayPrototypeTests
    {
        private GameObject _root, _cameraGo;

        [SetUp]
        public void SetUp()
        {
            if (Camera.main == null)
            {
                _cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
                _cameraGo.AddComponent<Camera>();
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root);
            if (_cameraGo != null) Object.Destroy(_cameraGo);
        }

        private PlopGameController CreateController(List<LevelDefinition> levels)
        {
            _root = new GameObject("PLOP Game (test)");
            var controller = _root.AddComponent<PlopGameController>();
            controller.RememberProgress = false;
            controller.UseLevels(levels);
            return controller;
        }

        private static void AssertViewsMatchState(PlopGameController c)
        {
            var state = c.Engine.CurrentState;
            for (int id = 0; id < state.PlopCount; id++)
            {
                var expected = c.Board.CellToWorld(c.Engine.Level.PositionOf(state.CellOf(id)));
                Assert.Less(Vector3.Distance(expected, c.Board.PlopViewPosition(id)), 0.001f, "Plop view " + id + " out of sync");
            }
        }

        private static IEnumerator WaitForAnimation(PlopGameController c)
        {
            for (int i = 0; i < 120 && c.Board.IsAnimating; i++) yield return null;
            Assert.IsFalse(c.Board.IsAnimating, "animation did not finish");
        }

        [UnityTest]
        public IEnumerator InputBuffersOneDirectionUntilTravelEnds()
        {
            var controller = CreateController(new List<LevelDefinition> { LevelText.Parse("queue", "R....r") });
            yield return null;

            controller.RequestMove(Direction.Right);
            controller.RequestMove(Direction.Right);
            controller.RequestMove(Direction.Left);
            Assert.AreEqual(1, controller.Engine.MoveCount);
            Assert.AreEqual(Direction.Right, controller.PendingMove);
            float deadline = Time.unscaledTime + 2f;
            while (controller.Engine.MoveCount < 2 && Time.unscaledTime < deadline)
                yield return null;
            Assert.AreEqual(2, controller.Engine.MoveCount);
            Assert.IsNull(controller.PendingMove);
            Assert.AreEqual(2, controller.Engine.Level.PositionOf(controller.Engine.CurrentState.CellOf(0)).X);
            yield return WaitForAnimation(controller);
            AssertViewsMatchState(controller);

            controller.SetReducedMotion(true);
            controller.Restart();
            controller.RequestMove(Direction.Right);
            controller.RequestMove(Direction.Right);
            Assert.AreEqual(2, controller.Engine.MoveCount);
            Assert.IsNull(controller.PendingMove);
            AssertViewsMatchState(controller);
        }

        [UnityTest]
        public IEnumerator InputQueueClearsOnUndoRestartPauseAndLevelLoad()
        {
            var first = new List<LevelDefinition> { LevelText.Parse("queue-reset", "R....r") };
            var second = new List<LevelDefinition> { LevelText.Parse("new-level", "R...r") };
            var controller = CreateController(first);
            yield return null;

            controller.RequestMove(Direction.Right);
            controller.RequestMove(Direction.Right);
            controller.Undo();
            Assert.IsNull(controller.PendingMove);
            Assert.AreEqual(0, controller.Engine.MoveCount);
            yield return WaitForAnimation(controller);
            AssertViewsMatchState(controller);

            controller.RequestMove(Direction.Right);
            controller.RequestMove(Direction.Right);
            controller.Restart();
            Assert.IsNull(controller.PendingMove);
            Assert.AreEqual(0, controller.Engine.MoveCount);
            AssertViewsMatchState(controller);

            controller.RequestMove(Direction.Right);
            controller.RequestMove(Direction.Right);
            Assert.NotNull(controller.PendingMove);
            controller.SendMessage("OnApplicationPause", true);
            controller.RequestMove(Direction.Right);
            Assert.IsNull(controller.PendingMove);
            Assert.AreEqual(1, controller.Engine.MoveCount);
            controller.SendMessage("OnApplicationPause", false);
            yield return WaitForAnimation(controller);
            Assert.AreEqual(1, controller.Engine.MoveCount);
            AssertViewsMatchState(controller);

            controller.RequestMove(Direction.Right);
            controller.RequestMove(Direction.Right);
            Assert.NotNull(controller.PendingMove);
            controller.UseLevels(second);
            Assert.IsNull(controller.PendingMove);
            Assert.AreEqual(0, controller.Engine.MoveCount);
            AssertViewsMatchState(controller);

            controller.RequestMove(Direction.Right);
            controller.RequestMove(Direction.Right);
            Assert.NotNull(controller.PendingMove);
            controller.enabled = false;
            controller.RequestMove(Direction.Right);
            controller.Move(Direction.Right);
            Assert.IsNull(controller.PendingMove);
            Assert.AreEqual(1, controller.Engine.MoveCount);
            controller.enabled = true;
            yield return WaitForAnimation(controller);
            AssertViewsMatchState(controller);
        }

        [UnityTest]
        public IEnumerator PlayLevel_MoveAnimateCompleteUndoRestartNext()
        {
            var levels = new List<LevelDefinition>
            {
                LevelText.Parse("t1", "R..r", "B#.b"),
                LevelText.Parse("t2", "R.r")
            };
            var c = CreateController(levels);
            yield return null;
            AssertViewsMatchState(c);

            c.Move(Direction.Right);           // R -> x1, B blocked by wall (bump animation)
            Assert.IsTrue(c.Board.IsAnimating);
            yield return WaitForAnimation(c);
            AssertViewsMatchState(c);
            Assert.AreEqual(1, c.Engine.MoveCount);

            c.Move(Direction.Right);
            c.Move(Direction.Right);           // second move interrupts the animation; views must still end in sync
            yield return WaitForAnimation(c);
            AssertViewsMatchState(c);
            Assert.IsTrue(c.Engine.CurrentState.IsAsleep(0), "R should sleep on its nest");

            c.Undo();
            yield return null;
            AssertViewsMatchState(c);
            Assert.IsFalse(c.Engine.CurrentState.IsAsleep(0));

            c.Restart();
            yield return null;
            Assert.AreEqual(0, c.Engine.MoveCount);
            AssertViewsMatchState(c);

            // solve level 1 with the verified solver path and advance
            foreach (var d in Plop.Solver.BfsSolver.Solve(levels[0]).Moves) c.Move(d);
            yield return WaitForAnimation(c);
            Assert.IsTrue(c.Engine.IsCompleted);
            AssertViewsMatchState(c);

            c.NextLevel();
            yield return null;
            Assert.AreEqual(1, c.LevelIndex);
            Assert.AreEqual("t2", c.Engine.Level.Id);
            AssertViewsMatchState(c);
        }

        [UnityTest]
        public IEnumerator ShippedPack_LoadsFromResources_AndFirstLevelIsPlayable()
        {
            var levels = LevelLibrary.Load();
            Assert.AreEqual(160, levels.Count, "PLOP_Pack_002 should contain 160 levels");
            Assert.IsNull(levels[0].Metadata, "runtime load must skip developer metadata");
            var c = CreateController(levels);
            yield return null;
            Assert.IsTrue(c.Engine.HasLevel);
            Assert.AreEqual(levels[0].Id, c.Engine.Level.Id);
        }

        [UnityTest]
        public IEnumerator Campaign_UnlocksOnCompletion_AndCanReplayWithoutLosingProgress()
        {
            var c = CreateController(new List<LevelDefinition>
            {
                LevelText.Parse("campaign-1", "R.r"),
                LevelText.Parse("campaign-2", "B.b")
            });
            yield return null;
            Assert.IsTrue(c.IsLevelUnlocked(0));
            Assert.IsFalse(c.TrySelectLevel(1));
            c.NextLevel();
            Assert.AreEqual(0, c.LevelIndex);

            c.Move(Direction.Right);
            c.Move(Direction.Right);
            Assert.IsTrue(c.IsLevelCompleted(0));
            Assert.IsTrue(c.IsLevelUnlocked(1));

            c.Undo();
            Assert.IsFalse(c.Engine.IsCompleted);
            Assert.IsTrue(c.IsLevelCompleted(0), "undo should only affect the active board");
            c.NextLevel();
            Assert.AreEqual(0, c.LevelIndex, "next requires the active board to be solved");
            Assert.IsTrue(c.TrySelectLevel(1));

            c.Move(Direction.Right);
            c.Move(Direction.Right);
            Assert.IsTrue(c.IsLevelCompleted(1));
            c.NextLevel();
            Assert.AreEqual(1, c.LevelIndex, "the last level does not wrap around");

            Assert.IsTrue(c.TrySelectLevel(0));
            Assert.IsFalse(c.Engine.IsCompleted);
            Assert.AreEqual(0, c.Engine.MoveCount);
            Assert.IsTrue(c.IsLevelCompleted(0));
            Assert.IsTrue(c.IsLevelCompleted(1));
        }

        [UnityTest]
        public IEnumerator Hint_UsesCurrentBoard_AndDiscardsStaleSearch()
        {
            var level = LevelText.Parse("hint-test", "R.r", ".##");
            var c = CreateController(new List<LevelDefinition> { level });
            yield return null;
            Assert.AreEqual(Direction.Right, Plop.Solver.BfsSolver.Solve(level).Moves[0]);

            c.Move(Direction.Down);
            var snapshot = c.Engine.GetState();
            var hint = c.RequestHintAsync();
            yield return WaitForTask(hint);
            Assert.AreEqual(Direction.Up, hint.Result);
            Assert.AreEqual(snapshot, c.Engine.GetState(), "asking for a hint must not move the board");

            var pending = c.RequestHintAsync();
            bool finishedBeforeRestart = pending.IsCompleted;
            c.Restart();
            yield return WaitForTask(pending);
            if (!finishedBeforeRestart) Assert.IsNull(pending.Result, "a stale hint must be discarded");

            var restartedHint = c.RequestHintAsync();
            yield return WaitForTask(restartedHint);
            Assert.AreEqual(Direction.Right, restartedHint.Result);
        }

        [UnityTest]
        public IEnumerator Hint_IfUnsolvable_ReturnsNoDirection()
        {
            var c = CreateController(new List<LevelDefinition> { LevelText.Parse("hint-dead-end", "R#r") });
            yield return null;
            var hint = c.RequestHintAsync();
            yield return WaitForTask(hint);
            Assert.IsNull(hint.Result);
            Assert.AreEqual(0, c.Engine.MoveCount);
        }

        private static IEnumerator WaitForTask(Task task)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(task.IsCompleted, "hint task did not finish");
        }

        [UnityTest]
        public IEnumerator SavedCampaign_MigratesLegacyAccess_AndPersistsCompletionAndFrameRate()
        {
            const string progress = "plop.progress.v1." + LevelLibrary.DefaultPack;
            const string legacyIndex = "plop.levelIndex";
            const string frameRate = "plop.frameRate";
            const string debugLevel = "plop.debug.level";
            const string debugMoves = "plop.debug.moves";
            const string previousProgress = "plop.progress.v1." + LevelLibrary.PreviousPack;

            string savedProgress = PlayerPrefs.HasKey(progress) ? PlayerPrefs.GetString(progress) : null;
            string savedPrevious = PlayerPrefs.HasKey(previousProgress) ? PlayerPrefs.GetString(previousProgress) : null;
            int? savedIndex = PlayerPrefs.HasKey(legacyIndex) ? PlayerPrefs.GetInt(legacyIndex) : (int?)null;
            int? savedRate = PlayerPrefs.HasKey(frameRate) ? PlayerPrefs.GetInt(frameRate) : (int?)null;
            int? savedDebugLevel = PlayerPrefs.HasKey(debugLevel) ? PlayerPrefs.GetInt(debugLevel) : (int?)null;
            string savedDebugMoves = PlayerPrefs.HasKey(debugMoves) ? PlayerPrefs.GetString(debugMoves) : null;

            try
            {
                PlayerPrefs.DeleteKey(progress);
                PlayerPrefs.DeleteKey(previousProgress);
                PlayerPrefs.DeleteKey(debugLevel);
                PlayerPrefs.DeleteKey(debugMoves);
                PlayerPrefs.SetInt(legacyIndex, 4);
                _root = new GameObject("PLOP save test");
                var controller = _root.AddComponent<PlopGameController>();
                yield return null;

                for (int i = 0; i < 5; i++)
                {
                    Assert.IsTrue(controller.IsLevelUnlocked(i), "legacy access lost for level " + (i + 1));
                    Assert.IsFalse(controller.IsLevelCompleted(i), "legacy index is not a completion record");
                }

                Assert.IsTrue(controller.TrySelectLevel(0));
                foreach (var move in Plop.Solver.BfsSolver.Solve(controller.Engine.Level).Moves)
                    controller.Move(move);
                Assert.IsTrue(controller.IsLevelCompleted(0));
                controller.SetFrameRateMode(PlopGameController.FrameRateMode.Fps30);
                Assert.AreEqual(30, Application.targetFrameRate);

                Object.Destroy(_root);
                yield return null;
                _root = new GameObject("PLOP save reload test");
                controller = _root.AddComponent<PlopGameController>();
                yield return null;
                Assert.IsTrue(controller.IsLevelCompleted(0), "completion should save before pressing next");
                Assert.AreEqual(1, controller.LevelIndex);
                Assert.IsTrue(controller.TrySelectLevel(0));
                Assert.IsFalse(controller.Engine.IsCompleted, "replay starts with an unsolved board");
                Assert.AreEqual(PlopGameController.FrameRateMode.Fps30, controller.CurrentFrameRateMode);
            }
            finally
            {
                RestorePreference(progress, savedProgress);
                RestorePreference(previousProgress, savedPrevious);
                RestorePreference(legacyIndex, savedIndex);
                RestorePreference(frameRate, savedRate);
                RestorePreference(debugLevel, savedDebugLevel);
                RestorePreference(debugMoves, savedDebugMoves);
                PlayerPrefs.Save();
            }
        }

        private static void RestorePreference(string key, string original)
        {
            if (original == null) PlayerPrefs.DeleteKey(key);
            else PlayerPrefs.SetString(key, original);
        }

        private static void RestorePreference(string key, int? original)
        {
            if (original.HasValue) PlayerPrefs.SetInt(key, original.Value);
            else PlayerPrefs.DeleteKey(key);
        }

        [Test]
        public void SwipeMapping_UsesDominantAxis_AndIgnoresTaps()
        {
            Assert.IsNull(SwipeInput.SwipeToDirection(new Vector2(3, 2), 20));
            Assert.AreEqual(Direction.Right, SwipeInput.SwipeToDirection(new Vector2(80, 30), 20));
            Assert.AreEqual(Direction.Left, SwipeInput.SwipeToDirection(new Vector2(-80, 30), 20));
            Assert.AreEqual(Direction.Up, SwipeInput.SwipeToDirection(new Vector2(10, 90), 20));
            Assert.AreEqual(Direction.Down, SwipeInput.SwipeToDirection(new Vector2(10, -90), 20));
        }
    }
}
