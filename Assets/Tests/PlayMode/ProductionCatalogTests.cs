using System.Collections;
using NUnit.Framework;
using Plop.Core;
using Plop.Presentation;
using Plop.Solver;
using UnityEngine;
using UnityEngine.TestTools;

namespace Plop.Tests.PlayMode
{
    public class ProductionCatalogTests
    {
        [UnityTest]
        public IEnumerator EveryShippedBoard_LoadsPlaysUndoesRestartsAndAdvances()
        {
            var levels = LevelLibrary.Load();
            Assert.AreEqual(100, levels.Count);
            var camera = new GameObject("Catalog camera") { tag = "MainCamera" };
            camera.AddComponent<Camera>();
            var root = new GameObject("Catalog test");
            try
            {
                var controller = root.AddComponent<PlopGameController>();
                controller.RememberProgress = false;
                controller.UseLevels(levels);
                yield return null;

                for (int i = 0; i < levels.Count; i++)
                {
                    var level = levels[i];
                    string label = level.Name + " " + level.Id;
                    Assert.AreEqual(level.Id, controller.Engine.Level.Id, label);
                    var initial = controller.Engine.GetState();
                    var solution = BfsSolver.Solve(level);
                    Assert.AreEqual(SolverStatus.Solved, solution.Status, label);
                    foreach (var direction in solution.Moves) controller.Move(direction);
                    Assert.IsTrue(controller.Engine.IsCompleted, label);
                    Assert.AreEqual(solution.Moves.Length, controller.Engine.MoveCount, label);

                    controller.Undo();
                    Assert.IsFalse(controller.Engine.IsCompleted, label);
                    Assert.AreEqual(solution.Moves.Length - 1, controller.Engine.MoveCount, label);
                    controller.Restart();
                    Assert.AreEqual(initial, controller.Engine.CurrentState, label);
                    Assert.IsFalse(controller.Engine.CanUndo, label);
                    Assert.AreEqual(0, controller.Engine.MoveCount, label);

                    foreach (var direction in solution.Moves) controller.Move(direction);
                    Assert.IsTrue(controller.Engine.IsCompleted, label);
                    controller.NextLevel();
                    yield return null;
                }
                Assert.AreEqual(levels.Count - 1, controller.LevelIndex, "The last level must not wrap to Level 1.");
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(camera);
            }
        }
    }
}
