using System.Collections;
using System.Collections.Generic;
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
            Assert.AreEqual(100, levels.Count, "PLOP_Pack_001 should contain 100 levels");
            Assert.IsNull(levels[0].Metadata, "runtime load must skip developer metadata");
            var c = CreateController(levels);
            yield return null;
            Assert.IsTrue(c.Engine.HasLevel);
            Assert.AreEqual(levels[0].Id, c.Engine.Level.Id);
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
