using System.Collections;
using NUnit.Framework;
using Plop.Core;
using Plop.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Plop.Tests.PlayMode
{
    public class PlopMotionFeedbackTests
    {
        [Test]
        public void WallAndPlopBlocks_KeepTheCellAndUseDistinctRecoil()
        {
            var scene = new GameObject("Block profiles");
            try
            {
                var atlas = ShapeAtlas.ForDensity(96f);
                var wall = new PlopView(scene.transform, atlas, 0, 1);
                var soft = new PlopView(scene.transform, atlas, 0, 1);
                wall.Snap(Vector3.zero, false);
                soft.Snap(Vector3.zero, false);
                wall.StartBump(Vector2.right, 10f, BlockReason.Wall);
                soft.StartBump(Vector2.right, 10f, BlockReason.Plop);
                wall.Tick(10.078f);
                soft.Tick(10.078f);

                var wallPose = wall.Root.Find("Pose");
                var softPose = soft.Root.Find("Pose");
                Assert.AreEqual(Vector3.zero, wall.Root.localPosition);
                Assert.AreEqual(Vector3.zero, soft.Root.localPosition);
                Assert.That(wallPose.localPosition.x, Is.GreaterThan(softPose.localPosition.x + 0.02f));
                Assert.That(softPose.localPosition.x, Is.GreaterThan(0.02f));
                Assert.That(wallPose.Find("Body").localScale.x, Is.LessThan(softPose.Find("Body").localScale.x));
                Assert.AreSame(atlas.Get("mouthBump"), wallPose.Find("Body/Mouth").GetComponent<SpriteRenderer>().sprite);
                Assert.AreSame(atlas.Get("mouth"), softPose.Find("Body/Mouth").GetComponent<SpriteRenderer>().sprite);

                wall.Tick(10.17f);
                soft.Tick(10.17f);
                Assert.IsTrue(wall.IsMoving);
                Assert.IsFalse(soft.IsMoving);
            }
            finally { Object.Destroy(scene); }
        }

        [Test]
        public void MoveInterruptedByRetargetAndUndo_PreservesPoseAndRetiresOldEndpoints()
        {
            var scene = new GameObject("Interruptions");
            try
            {
                var view = new PlopView(scene.transform, ShapeAtlas.ForDensity(96f), 0, 1);
                var pose = view.Root.Find("Pose");
                var body = pose.Find("Body");
                view.Snap(Vector3.zero, false);
                view.StartBump(Vector2.right, 10f, BlockReason.Wall);
                view.Tick(10.078f);
                var beforeOffset = pose.localPosition;
                var beforeScale = body.localScale;

                view.StartMove(Vector3.up, Vector2.up, 10.078f);
                view.Tick(10.078f);
                Assert.AreEqual(beforeOffset, pose.localPosition, "first retarget frame must carry the existing recoil");
                Assert.AreEqual(beforeScale, body.localScale);
                view.Tick(10.14f);

                var visiblePosition = view.Root.localPosition + pose.localPosition;
                Assert.That(visiblePosition.y, Is.InRange(0.1f, 0.8f));
                view.Restore(Vector3.zero, false, ease: true);
                Assert.AreEqual(Vector3.zero, view.Root.localPosition, "Undo restores the exact cell immediately");
                Assert.That(Vector3.Distance(visiblePosition, pose.localPosition), Is.LessThan(0.001f));

                view.Snap(Vector3.right, false);
                view.Tick(41f);
                Assert.AreEqual(Vector3.right, view.Root.localPosition);
                Assert.AreEqual(Vector3.zero, pose.localPosition);
                Assert.IsFalse(view.IsBusy(41f));
            }
            finally { Object.Destroy(scene); }
        }

        [UnityTest]
        public IEnumerator BoardTravelGate_DoesNotWaitForRecoilSleepOrUndoEase()
        {
            var scene = new GameObject("Travel gate");
            try
            {
                var board = scene.AddComponent<BoardView>();
                var atlas = ShapeAtlas.ForDensity(96f);
                var engine = new GameEngine();
                engine.LoadLevel(LevelText.Parse("travel-gate", "R.r"));
                board.Build(engine.Level, engine.CurrentState, atlas);
                board.Play(engine.TryMove(Direction.Left).Events);
                Assert.IsFalse(board.IsTraveling);
                Assert.IsTrue(board.IsAnimating);

                board.Play(engine.TryMove(Direction.Right).Events);
                Assert.IsTrue(board.IsTraveling);
                float limit = Time.unscaledTime + 1f;
                while (board.IsTraveling && Time.unscaledTime < limit) yield return null;
                Assert.IsFalse(board.IsTraveling);
                Assert.IsTrue(board.IsBusy, "landing takes longer than travel");

                board.Play(engine.TryMove(Direction.Right).Events);
                Assert.IsTrue(board.IsTraveling);
                limit = Time.unscaledTime + 1f;
                while (board.IsTraveling && Time.unscaledTime < limit) yield return null;
                Assert.IsFalse(board.IsTraveling);
                Assert.IsTrue(board.IsBusy, "sleep and Nest response must leave the gate clear");

                engine.Undo();
                board.Sync(engine.CurrentState, easeRestore: true);
                Assert.IsFalse(board.IsTraveling);
                Assert.AreEqual(board.CellToWorld(new GridPosition(1, 0)), board.PlopViewPosition(0));
                board.SetReducedMotion(true);
                board.Play(engine.TryMove(Direction.Right).Events);
                Assert.IsFalse(board.IsTraveling);
                Assert.IsFalse(board.IsBusy);
                Assert.AreEqual(board.CellToWorld(new GridPosition(2, 0)), board.PlopViewPosition(0));
            }
            finally { Object.Destroy(scene); }
        }
    }
}
