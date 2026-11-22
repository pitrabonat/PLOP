using System;
using NUnit.Framework;
using Plop.Core;
using Plop.Presentation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Plop.Tests.PlayMode
{
    public class JellyWorldVisualTests
    {
        [Test]
        public void MatchSeals_UseDistinctSilhouettesAndOneAtlas()
        {
            var atlas = ShapeAtlas.ForDensity(65);
            var masks = new bool[PlopColorUtil.Count][];
            for (int color = 0; color < PlopColorUtil.Count; color++)
            {
                Assert.AreSame(atlas.Texture, atlas.Get(ShapeAtlas.NestSealKey(color)).texture);
                Assert.AreSame(atlas.Texture, atlas.Get(ShapeAtlas.PlopKey(color)).texture);
                var mask = masks[color] = new bool[256];
                for (int i = 0; i < mask.Length; i++)
                {
                    float x = (i % 16 - 7.5f) * 0.12f;
                    float y = (i / 16 - 7.5f) * 0.12f;
                    mask[i] = ShapeAtlas.MatchMarkDistance(color, x, y) < 0f;
                }
                Assert.That(Array.FindAll(mask, covered => covered).Length, Is.GreaterThan(12));
                for (int other = 0; other < color; other++)
                {
                    int difference = 0;
                    for (int i = 0; i < mask.Length; i++)
                        if (mask[i] != masks[other][i]) difference++;
                    Assert.That(difference, Is.GreaterThan(8), $"Seals {color} and {other} must differ without hue.");
                }
            }
        }

        [Test]
        public void ReducedMotion_ResolvesInFlightTravelAndSleepAndSuppressesFeedbackMotion()
        {
            var scene = new GameObject("Reduced motion test");
            try
            {
                var atlas = ShapeAtlas.ForDensity(90f);
                var view = new PlopView(scene.transform, atlas, colorId: 0, seed: 3);
                var pose = view.Root.Find("Pose");
                var body = pose.Find("Body");
                var eye = body.Find("EyeL").GetComponent<SpriteRenderer>();
                view.Snap(Vector3.zero, false);
                view.StartMove(Vector3.right, Vector2.right, 10f);
                view.QueueSleep(10f);
                view.Tick(10.08f);
                Assert.IsTrue(view.IsMoving);
                view.SetReducedMotion(true);
                Assert.AreEqual(Vector3.right, view.Root.localPosition);
                Assert.AreSame(atlas.Get("eyeClosed"), eye.sprite);
                Assert.AreEqual(Vector3.zero, pose.localPosition);
                Assert.IsFalse(view.IsBusy(10.08f));

                view.Restore(Vector3.zero, false, ease: true);
                view.StartBump(Vector2.up, 11f);
                view.StartHop(11f);
                var still = body.localScale;
                view.Tick(56f);
                Assert.AreEqual(Vector3.zero, pose.localPosition);
                Assert.AreEqual(still, body.localScale);
                Assert.AreSame(atlas.Get("eye"), eye.sprite);
                Assert.IsFalse(view.IsBusy(56f));
            }
            finally { Object.Destroy(scene); }
        }

        [Test]
        public void UndoEasing_PreservesTheRestoredCellAndClearsOnRestart()
        {
            var scene = new GameObject("Undo pose test");
            try
            {
                var atlas = ShapeAtlas.ForDensity(90f);
                var view = new PlopView(scene.transform, atlas, colorId: 0, seed: 3);
                var pose = view.Root.Find("Pose");
                view.Snap(Vector3.right, false);
                view.Restore(Vector3.zero, false, ease: true);
                Assert.AreEqual(Vector3.zero, view.Root.localPosition);
                Assert.AreEqual(Vector3.right, pose.localPosition);

                view.Tick(Time.unscaledTime + PlopView.RestoreDuration + 0.01f);
                Assert.AreEqual(Vector3.zero, pose.localPosition);
                Assert.AreEqual(Vector3.zero, view.Root.localPosition);

                view.Snap(Vector3.right, false);
                view.Restore(Vector3.zero, false, ease: true);
                view.Snap(Vector3.up, false);
                Assert.AreEqual(Vector3.up, view.Root.localPosition);
                Assert.AreEqual(Vector3.zero, pose.localPosition);
            }
            finally { Object.Destroy(scene); }
        }

        [Test]
        public void PaletteAndNestBadge_DoNotChangeGridOrHideAnOccupiedNest()
        {
            var scene = new GameObject("Board palette test");
            try
            {
                var board = scene.AddComponent<BoardView>();
                var engine = new GameEngine();
                engine.LoadLevel(LevelText.Parse("crossed-nests", "Rb.r", "B..#"));
                var atlas = ShapeAtlas.ForDensity(90f);
                board.SetReducedMotion(true);
                board.Build(engine.Level, engine.CurrentState, atlas);

                board.Play(engine.TryMove(Direction.Right).Events);
                var nest = scene.transform.Find("Nest Blue");
                var badge = nest.Find("Match seal Blue").GetComponent<SpriteRenderer>();
                var red = scene.transform.Find("Plop/Pose/Body").GetComponent<SpriteRenderer>();
                Assert.AreEqual(board.CellToWorld(new GridPosition(1, 1)), board.PlopViewPosition(0));
                Assert.IsFalse(engine.CurrentState.IsAsleep(0));
                Assert.Greater(badge.sortingOrder, red.sortingOrder);
                Assert.AreSame(atlas.Texture, badge.sprite.texture);

                var trayTint = new Color(0.80f, 0.94f, 0.93f, 1f);
                board.SetPalette(trayTint, Color.white, Color.white);
                Assert.AreEqual(trayTint, scene.transform.Find("BoardPanel").GetComponent<SpriteRenderer>().color);
                Assert.AreSame(atlas, board.Atlas);
                Assert.AreEqual(board.CellToWorld(new GridPosition(1, 1)), board.PlopViewPosition(0));
            }
            finally { Object.Destroy(scene); }
        }
    }
}
