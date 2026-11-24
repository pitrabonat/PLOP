using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Plop.Core;
using Plop.Presentation;
using Plop.Solver;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Plop.Tests.PlayMode
{
    public sealed class DeadlockRecoveryTests
    {
        private GameObject _root;
        private int _frameRate;
        private int _vSync;

        [SetUp]
        public void PreserveRenderSettings()
        {
            _frameRate = Application.targetFrameRate;
            _vSync = QualitySettings.vSyncCount;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            Application.targetFrameRate = _frameRate;
            QualitySettings.vSyncCount = _vSync;
        }

        [UnityTest]
        public IEnumerator ProductionTrapUsesOfflineProofAndRecoveryRestoresPositions()
        {
            var controller = CreateController();
            yield return null;
            controller.StartCampaign(); // dismiss the main menu with PLAY
            int fallbackCalls = 0;
            controller.DeadlockFallback = (level, state, token) =>
            {
                fallbackCalls++;
                return Task.FromResult(DeadlockStatus.Unknown);
            };

            for (int i = 0; i < 7; i++)
            {
                foreach (var direction in BfsSolver.Solve(controller.Engine.Level).Moves) controller.Move(direction);
                Assert.IsTrue(controller.Engine.IsCompleted);
                controller.NextLevel();
            }
            var hud = _root.GetComponent<GameHud>();
            var initial = controller.Engine.GetState();
            controller.Move(Direction.Up);
            var previous = controller.Engine.GetState();
            Assert.AreEqual(SolverStatus.Solved, BfsSolver.Solve(controller.Engine.Level, previous).Status);
            controller.Move(Direction.Left);
            var stuck = controller.Engine.GetState();
            Assert.AreEqual(SolverStatus.ProvenUnsolvable, BfsSolver.Solve(controller.Engine.Level, stuck).Status);
            Assert.That(MoveRuleDirections(controller.Engine.Level, stuck), Is.GreaterThan(0));
            yield return WaitFor(() => hud.IsDeadlockOpen, "production deadlock dialog");

            Assert.AreEqual(2, controller.Engine.MoveCount);
            controller.Move(Direction.Up);
            Assert.AreEqual(stuck, controller.Engine.CurrentState, "the modal must intercept board input");
            Click("Undo Move");
            Assert.IsFalse(hud.IsDeadlockOpen);
            Assert.AreEqual(previous, controller.Engine.CurrentState);
            Assert.AreEqual(1, controller.Engine.MoveCount);
            AssertViewsMatch(controller);

            controller.Move(Direction.Left);
            yield return WaitFor(() => hud.IsDeadlockOpen, "second proven deadlock");
            Click("Restart Level");
            Assert.IsFalse(hud.IsDeadlockOpen);
            Assert.AreEqual(initial, controller.Engine.CurrentState);
            Assert.AreEqual(0, controller.Engine.MoveCount);
            AssertViewsMatch(controller);
            Assert.IsTrue(controller.IsLevelCompleted(6), "recovery must preserve campaign progress");
            Assert.IsTrue(controller.IsLevelUnlocked(7));
            Assert.AreEqual(0, fallbackCalls, "the shipped proof should bypass fallback search");
        }

        [UnityTest]
        public IEnumerator DismissingAProofPreventsRepeatOnTheUnchangedBoard()
        {
            var controller = CreateController();
            yield return null;
            controller.StartCampaign(); // dismiss the main menu with PLAY
            for (int i = 0; i < 7; i++)
            {
                foreach (var direction in BfsSolver.Solve(controller.Engine.Level).Moves) controller.Move(direction);
                controller.NextLevel();
            }
            controller.SetReducedMotion(true);
            var hud = _root.GetComponent<GameHud>();
            controller.Move(Direction.Up);
            controller.Move(Direction.Left);
            yield return WaitFor(() => hud.IsDeadlockOpen, "proven Level 8 deadlock");
            var trapped = controller.Engine.GetState();

            Click("Keep playing");
            controller.SendMessage("CheckCurrentDeadlock");
            yield return null;
            yield return null;
            Assert.IsFalse(hud.IsDeadlockOpen);
            Assert.AreEqual(trapped, controller.Engine.CurrentState);

            controller.Undo();
            controller.Move(Direction.Left);
            yield return WaitFor(() => hud.IsDeadlockOpen, "new proof after a board change");
        }

        [UnityTest]
        public IEnumerator SearchLimitDoesNotOpenModalAndDismissDoesNotChangeState()
        {
            var controller = CreateController(LevelText.Parse("limited", "R...r"));
            var hud = _root.GetComponent<GameHud>();
            bool evaluated = false;
            controller.DeadlockFallback = (level, state, token) =>
            {
                var result = BfsSolver.Solve(level, state, new SolverOptions { MaxStates = 1 });
                Assert.AreEqual(SolverStatus.SearchLimitReached, result.Status);
                evaluated = true;
                return Task.FromResult(PlopGameController.ClassifyDeadlockSearchResult(result.Status));
            };
            controller.Move(Direction.Right);
            var stateBefore = controller.Engine.GetState();
            yield return WaitFor(() => evaluated, "budget-limited search");
            yield return new WaitForSecondsRealtime(0.45f);
            Assert.IsFalse(hud.IsDeadlockOpen);

            // Check the dismissal UI without requiring an unsolvable production start.
            controller.Restart();
            var initial = controller.Engine.GetState();
            Assert.IsTrue(hud.ShowDeadlock(false));
            Assert.IsTrue(FindButton("Restart Level").interactable);
            Assert.IsFalse(_root.GetComponentsInChildren<Button>().Any(button =>
                ButtonText(button) == "Undo Move" && button.interactable));
            Click("Keep playing");
            Assert.AreEqual(initial, controller.Engine.CurrentState);
            Assert.AreEqual(0, controller.Engine.MoveCount);
            Assert.IsFalse(hud.IsDeadlockOpen);
            Assert.AreNotEqual(stateBefore, controller.Engine.CurrentState);
        }

        [UnityTest]
        public IEnumerator UndoAndRestartDiscardAWorkerThatReturnsAfterCancellation()
        {
            var controller = CreateController(LevelText.Parse("restore", "R...r"));
            var hud = _root.GetComponent<GameHud>();
            var initial = controller.Engine.GetState();

            foreach (Action recover in new Action[] { controller.Undo, controller.Restart })
            {
                var pending = CapturePendingSearch(controller);
                controller.Move(Direction.Right);
                yield return WaitFor(() => pending.Started, "pending search");
                recover();
                Assert.IsTrue(pending.Token.IsCancellationRequested);
                pending.Result.SetResult(DeadlockStatus.ProvenDeadlock);
                yield return new WaitForSecondsRealtime(0.40f);
                Assert.IsFalse(hud.IsDeadlockOpen);
                Assert.AreEqual(initial, controller.Engine.CurrentState);
            }
        }

        [UnityTest]
        public IEnumerator CompletionAndLevelTransitionTakePriorityOverPendingProof()
        {
            var controller = CreateController(LevelText.Parse("first", "R.r"), LevelText.Parse("second", "R.r"));
            var hud = _root.GetComponent<GameHud>();
            var pending = CapturePendingSearch(controller);

            controller.Move(Direction.Right);
            yield return WaitFor(() => pending.Started, "proof before completion");
            controller.Move(Direction.Right);
            Assert.IsTrue(controller.Engine.IsCompleted);
            Assert.IsTrue(pending.Token.IsCancellationRequested);
            pending.Result.SetResult(DeadlockStatus.ProvenDeadlock);
            yield return new WaitForSecondsRealtime(0.40f);
            Assert.IsFalse(hud.IsDeadlockOpen);
            Assert.That(_root.GetComponentsInChildren<Text>().Any(text => text.text == "Sweet Dreams!"));
            controller.NextLevel();
            Assert.AreEqual(1, controller.LevelIndex);

            pending = CapturePendingSearch(controller);
            controller.Move(Direction.Right);
            yield return WaitFor(() => pending.Started, "proof on level two");
            Assert.IsTrue(controller.TrySelectLevel(0));
            Assert.IsTrue(pending.Token.IsCancellationRequested);
            pending.Result.SetResult(DeadlockStatus.ProvenDeadlock);
            yield return new WaitForSecondsRealtime(0.40f);
            Assert.IsFalse(hud.IsDeadlockOpen);
            Assert.AreEqual(0, controller.Engine.MoveCount);
        }

        [UnityTest]
        public IEnumerator ANewSwipeSupersedesTheOldAnalysis()
        {
            var controller = CreateController(LevelText.Parse("supersede", "R...r"));
            var pending = CapturePendingSearch(controller);
            controller.Move(Direction.Right);
            yield return WaitFor(() => pending.Started, "old search");

            bool rechecked = false;
            controller.DeadlockFallback = (level, state, token) =>
            {
                rechecked = true;
                return Task.FromResult(DeadlockStatus.Solvable);
            };
            controller.Move(Direction.Left);
            Assert.IsTrue(pending.Token.IsCancellationRequested);
            pending.Result.SetResult(DeadlockStatus.ProvenDeadlock);
            yield return WaitFor(() => rechecked, "new state search");
            Assert.IsFalse(_root.GetComponent<GameHud>().IsDeadlockOpen);
            Assert.AreEqual(2, controller.Engine.MoveCount);
        }

        private PlopGameController CreateController(params LevelDefinition[] levels)
        {
            _root = new GameObject("Deadlock recovery test");
            var controller = _root.AddComponent<PlopGameController>();
            controller.RememberProgress = false;
            if (levels.Length > 0) controller.UseLevels(new List<LevelDefinition>(levels));
            return controller;
        }

        private static int MoveRuleDirections(LevelDefinition level, GameState state)
        {
            int changed = 0;
            for (int d = 0; d < 4; d++)
                if (MoveRules.Apply(level, state.Clone(), (Direction)d)) changed++;
            return changed;
        }

        private static void AssertViewsMatch(PlopGameController controller)
        {
            var game = controller.Engine;
            for (int id = 0; id < game.CurrentState.PlopCount; id++)
            {
                var grid = game.Level.PositionOf(game.CurrentState.CellOf(id));
                var expected = controller.Board.CellToWorld(grid);
                Assert.That(Vector3.Distance(expected, controller.Board.PlopViewPosition(id)), Is.LessThan(0.001f));
            }
        }

        private static PendingSearch CapturePendingSearch(PlopGameController controller)
        {
            var pending = new PendingSearch();
            controller.DeadlockFallback = (level, state, token) =>
            {
                pending.Token = token;
                pending.Started = true;
                return pending.Result.Task;
            };
            return pending;
        }

        private static IEnumerator WaitFor(Func<bool> ready, string description)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(ready(), "Timed out waiting for " + description);
        }

        private void Click(string label) => FindButton(label).onClick.Invoke();

        private Button FindButton(string label) =>
            _root.GetComponentsInChildren<Button>().Single(button => ButtonText(button) == label);

        private static string ButtonText(Button button) => button.GetComponentInChildren<Text>()?.text;

        private sealed class PendingSearch
        {
            public readonly TaskCompletionSource<DeadlockStatus> Result =
                new TaskCompletionSource<DeadlockStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
            public bool Started;
            public CancellationToken Token;
        }
    }
}
