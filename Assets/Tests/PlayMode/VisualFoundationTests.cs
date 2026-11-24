using NUnit.Framework;
using Plop.Presentation;
using UnityEngine;

namespace Plop.Tests.PlayMode
{
    /// <summary>Rounded geometry, atlas batching and responsive layout of the visual foundation.</summary>
    public class VisualFoundationTests
    {
        [Test]
        public void CornerRadii_AreWithinTheStyleGuide()
        {
            float tile = ShapeAtlas.TileRadius / ShapeAtlas.TileSize;
            float pebble = ShapeAtlas.PebbleRadius / ShapeAtlas.PebbleSize;
            Assert.That(tile, Is.InRange(0.16f, 0.22f), "floor tile radius should be 16–22% of tile width");
            Assert.That(pebble, Is.InRange(0.18f, 0.25f), "pebble radius should be 18–25% of block width");
        }

        [Test]
        public void TileOutline_IsGeometricallyRounded()
        {
            float h = ShapeAtlas.TileSize * 0.5f;
            Assert.Less(ShapeAtlas.TileDistance(0f, 0f), 0f, "center is inside");
            Assert.Less(ShapeAtlas.TileDistance(h - 0.01f, 0f), 0f, "edge midpoint is inside");
            // A sharp square would contain its corner point; a rounded one must not.
            Assert.Greater(ShapeAtlas.TileDistance(h - 0.005f, h - 0.005f), 0.03f, "corner must be cut away by the radius");
            Assert.Greater(ShapeAtlas.PebbleTopDistance(ShapeAtlas.PebbleSize * 0.5f - 0.005f, ShapeAtlas.PebbleSize * 0.465f - 0.005f), 0.03f);
            // the tile never touches the cell border (visible gap between tiles)
            Assert.Greater(ShapeAtlas.TileDistance(0.5f, 0f), 0f);
        }

        [Test]
        public void MascotOutline_HasADomedCrownAndScallopedFoot()
        {
            Assert.Less(ShapeAtlas.BodyDistance(-0.235f, 0.64f), 0f, "the dome leans slightly");
            Assert.Greater(ShapeAtlas.BodyDistance(0.235f, 0.64f), 0.03f);
            Assert.Less(ShapeAtlas.BodyDistance(0f, 0.024f), 0f, "the middle foot touches the ground");
            Assert.Greater(ShapeAtlas.BodyDistance(0.355f, 0.024f), 0.014f, "the foot curls off the rounded lower corners");
            Assert.Greater(ShapeAtlas.BodyDistance(-0.145f, 0.005f), 0f, "the feet have shallow separations");
            Assert.Less(ShapeAtlas.BodyDistance(0.36f, 0.15f), 0f, "the body fills most of the tile width");
            Assert.Greater(ShapeAtlas.BodyDistance(0.405f, 0.15f), 0f);
        }

        [Test]
        public void Atlas_SharesOneTexture_ForBatching_AndScalesWithDensity()
        {
            var low = ShapeAtlas.ForDensity(90f);
            var high = ShapeAtlas.ForDensity(250f);
            Assert.Less(low.PixelsPerUnit, high.PixelsPerUnit);
            var tex = low.Texture;
            foreach (var key in new[] { "tile", "pebble", "panel", "panelShadow", "plopShadow", "eye", "eyeClosed", "cheek",
                                        "mouth", "mouthBump", "mouthSleep", ShapeAtlas.NestKey(0), ShapeAtlas.PlopKey(2),
                                        ShapeAtlas.WorriedPlopKey })
                Assert.AreSame(tex, low.Get(key).texture, key + " must live in the shared atlas texture");
            Assert.AreSame(low, ShapeAtlas.ForDensity(90f), "atlases are cached per density bucket");
            Assert.Greater(low.Get("panel").border.x, 0f, "board panel is 9-sliced");
            Assert.AreEqual(1f, low.Get("tile").rect.width / low.PixelsPerUnit, 0.02f, "a tile sprite spans exactly one cell");
        }

        [Test]
        public void UiSprites_AreRoundedNineSlices()
        {
            Assert.Greater(UiSprites.Pill.border.x, 0f);
            Assert.Greater(UiSprites.Card.border.y, 0f);
        }

        [Test]
        public void PlopRecoil_LeavesTheRootOnTheGridAndSnapsItsExpression()
        {
            var scene = new GameObject("Recoil test");
            try
            {
                var atlas = ShapeAtlas.ForDensity(90f);
                var view = new PlopView(scene.transform, atlas, colorId: 0, seed: 2);
                var pose = view.Root.Find("Pose");
                var mouth = pose.Find("Body/Mouth").GetComponent<SpriteRenderer>();
                var eye = pose.Find("Body/EyeL").GetComponent<SpriteRenderer>();
                var cell = new Vector3(1f, 2f, 0f);
                view.Snap(cell, false);

                view.StartBump(Vector2.right, 10f);
                view.Tick(10.078f);
                Assert.AreEqual(cell, view.Root.localPosition);
                Assert.Greater(pose.localPosition.x, 0.05f);
                Assert.AreSame(atlas.Get("mouthBump"), mouth.sprite);

                view.Tick(10.131f);
                Assert.AreEqual(cell, view.Root.localPosition);
                Assert.Less(pose.localPosition.x, 0f, "rebound briefly overshoots the resting pose");

                view.Snap(cell, true);
                Assert.AreEqual(Vector3.zero, pose.localPosition);
                Assert.AreSame(atlas.Get("eyeClosed"), eye.sprite);
                Assert.AreSame(atlas.Get("mouthSleep"), mouth.sprite);

                view.Snap(cell, false);
                Assert.AreSame(atlas.Get("eye"), eye.sprite);
                Assert.AreSame(atlas.Get("mouth"), mouth.sprite);
            }
            finally
            {
                Object.Destroy(scene);
            }
        }

        [Test]
        public void PlopMove_CompressesBeforeTravelAndCanRetargetMidSlide()
        {
            var scene = new GameObject("Slide test");
            try
            {
                var view = new PlopView(scene.transform, ShapeAtlas.ForDensity(90f), colorId: 1, seed: 3);
                var body = view.Root.Find("Pose/Body");
                view.Snap(Vector3.zero, false);
                view.StartMove(Vector3.right, Vector2.right, 20f);
                view.Tick(20.012f);
                Assert.AreEqual(Vector3.zero, view.Root.localPosition);
                Assert.Less(body.localScale.x, body.localScale.y, "horizontal anticipation squashes the toy");

                view.Tick(20.070f);
                var inFlight = view.Root.localPosition;
                Assert.That(inFlight.x, Is.InRange(0.15f, 0.98f));
                var newCell = new Vector3(1f, 1f, 0f);
                view.StartMove(newCell, Vector2.up, 20.070f);
                view.Tick(20.071f);
                Assert.AreEqual(inFlight, view.Root.localPosition);
                view.Tick(20.250f);
                Assert.AreEqual(newCell, view.Root.localPosition);
                Assert.IsFalse(view.IsMoving);
            }
            finally
            {
                Object.Destroy(scene);
            }
        }

        [Test]
        public void PlopSleep_WaitsForLandingAndUndoClearsThePendingCelebration()
        {
            var scene = new GameObject("Sleep test");
            try
            {
                var atlas = ShapeAtlas.ForDensity(90f);
                var view = new PlopView(scene.transform, atlas, colorId: 0, seed: 1);
                var eye = view.Root.Find("Pose/Body/EyeL").GetComponent<SpriteRenderer>();
                var mouth = view.Root.Find("Pose/Body/Mouth").GetComponent<SpriteRenderer>();
                view.Snap(Vector3.zero, false);
                view.StartMove(Vector3.up, Vector2.up, 30f);
                view.QueueSleep(30f);
                view.StartHop(30.23f);

                view.Tick(30.08f);
                Assert.IsFalse(view.SleepJustStarted);
                Assert.AreSame(atlas.Get("eye"), eye.sprite);

                view.Tick(30.16f);
                Assert.IsTrue(view.SleepJustStarted);
                Assert.AreEqual(Vector3.up, view.Root.localPosition);
                view.Tick(30.36f);
                Assert.AreSame(atlas.Get("eyeClosed"), eye.sprite);
                Assert.AreSame(atlas.Get("mouthSleep"), mouth.sprite);

                view.Snap(Vector3.zero, false);
                view.Tick(30.37f);
                Assert.AreEqual(Vector3.zero, view.Root.localPosition);
                Assert.IsFalse(view.IsBusy(30.37f));
                Assert.AreSame(atlas.Get("eye"), eye.sprite);
            }
            finally
            {
                Object.Destroy(scene);
            }
        }

        [Test]
        public void PlopSleep_StopsAfterSettlementAndWakesAfterUndo()
        {
            var scene = new GameObject("Still sleep test");
            try
            {
                var atlas = ShapeAtlas.ForDensity(90f);
                var view = new PlopView(scene.transform, atlas, colorId: 2, seed: 4);
                var body = view.Root.Find("Pose/Body");
                var eye = body.Find("EyeL").GetComponent<SpriteRenderer>();
                var cell = new Vector3(0f, 1f, 0f);
                view.Snap(cell, false);
                view.QueueSleep(40f);
                view.Tick(40f);
                Assert.IsTrue(view.SleepJustStarted);
                view.Tick(40.5f);
                var settledScale = body.localScale;
                var settledRotation = body.localRotation;
                view.Tick(43f);
                Assert.AreEqual(settledScale, body.localScale);
                Assert.AreEqual(settledRotation, body.localRotation);
                Assert.IsFalse(view.SleepJustStarted);
                Assert.AreSame(atlas.Get("eyeClosed"), eye.sprite);

                view.Snap(Vector3.zero, false);
                Assert.AreSame(atlas.Get("eye"), eye.sprite);
                Assert.AreEqual(Vector3.zero, view.Root.localPosition);
            }
            finally
            {
                Object.Destroy(scene);
            }
        }

        private static readonly (int w, int h, Rect safe, string name)[] Screens =
        {
            (1080, 1920, new Rect(0, 0, 1080, 1920), "16:9 no cutout"),
            (1179, 2556, new Rect(0, 102, 1179, 2556 - 102 - 177), "iPhone 15 Pro (Dynamic Island)"),
            (1290, 2796, new Rect(0, 102, 1290, 2796 - 102 - 177), "iPhone 15 Pro Max"),
            (1080, 2400, new Rect(0, 0, 1080, 2400 - 110), "20:9 Android punch-hole"),
            (1440, 3200, new Rect(0, 60, 1440, 3200 - 60 - 130), "QHD+ Android"),
            (720, 1280, new Rect(0, 0, 720, 1280), "small 720p"),
            (1536, 2048, new Rect(0, 40, 1536, 2048 - 40 - 48), "iPad 3:4"),
        };

        [Test]
        public void Layout_FitsEveryBoardInsideTheFreeSafeArea([Values(4, 5, 7)] int gridW, [Values(4, 6, 7)] int gridH)
        {
            foreach (var s in Screens)
            {
                var r = BoardLayout.Compute(s.w, s.h, s.safe, gridW, gridH);
                var board = r.BoardRectPixels;
                const float eps = 0.5f;
                Assert.Greater(r.PixelsPerUnit, 20f, s.name);
                Assert.GreaterOrEqual(board.xMin, r.AvailablePixels.xMin - eps, s.name + " left");
                Assert.LessOrEqual(board.xMax, r.AvailablePixels.xMax + eps, s.name + " right");
                Assert.GreaterOrEqual(board.yMin, r.AvailablePixels.yMin - eps, s.name + " overlaps footer");
                Assert.LessOrEqual(board.yMax, r.AvailablePixels.yMax + eps, s.name + " overlaps header");
                Assert.GreaterOrEqual(board.xMin, s.w * BoardLayout.SideMarginFraction - eps, s.name + " side margin");
                Assert.LessOrEqual(board.yMax, s.safe.yMax, s.name + " under the notch");
                // camera math: the board center (world origin) projects to the center of the board rect
                float centerPx = s.w * 0.5f + (0f - r.CameraPosition.x) * r.PixelsPerUnit;
                Assert.AreEqual(board.center.x, centerPx, 1f, s.name);
                Assert.AreEqual(s.h, 2f * r.OrthographicSize * r.PixelsPerUnit, 1f, s.name + " ortho size");
            }
        }

        [Test]
        public void Layout_DoesNotBlowUpSmallBoards()
        {
            var small = BoardLayout.Compute(1080, 1920, new Rect(0, 0, 1080, 1920), 4, 4);
            var large = BoardLayout.Compute(1080, 1920, new Rect(0, 0, 1080, 1920), 7, 7);
            Assert.Greater(small.PixelsPerUnit, large.PixelsPerUnit);
            Assert.LessOrEqual(small.BoardRectPixels.width, 1080 * 0.95f, "4x4 keeps a slim side margin");
        }
    }
}
