using NUnit.Framework;
using Plop.Presentation;
using UnityEngine;

namespace Plop.Tests.PlayMode
{
    public sealed class ResponsivePortraitTests
    {
        [TestCase(1080, 1920, 0, 0)]
        [TestCase(1170, 2532, 102, 141)]
        [TestCase(1440, 3200, 60, 130)]
        [TestCase(720, 1280, 0, 32)]
        public void SmallTrayUsesMostOfTheSafeWidthAndLargerGridsReflow(int w, int h, int bottom, int top)
        {
            var safe = new Rect(0, bottom, w, h - bottom - top);
            var small = BoardLayout.Compute(w, h, safe, 4, 4);
            Assert.That(small.BoardRectPixels.width / safe.width, Is.InRange(0.90f, 0.95f)); // Phase 8: small boards use most of the width
            AssertFits(small, 4, 4);

            var taller = BoardLayout.Compute(w, h, safe, 5, 9);
            var wider = BoardLayout.Compute(w, h, safe, 9, 5);
            Assert.That(taller.PixelsPerUnit, Is.LessThan(small.PixelsPerUnit));
            Assert.That(wider.PixelsPerUnit, Is.LessThan(small.PixelsPerUnit));
            AssertFits(taller, 5, 9);
            AssertFits(wider, 9, 5);
        }

        [Test]
        public void BoardRemainsCenteredUnderAsymmetricSafeInsets()
        {
            var safe = Rect.MinMaxRect(46, 80, 1420, 3004);
            var layout = BoardLayout.Compute(1440, 3200, safe, 5, 7);
            AssertFits(layout, 5, 7);
            Assert.That(layout.BoardRectPixels.center.x, Is.EqualTo(safe.center.x).Within(0.5f));
            Assert.That(layout.BoardRectPixels.yMin, Is.GreaterThanOrEqualTo(safe.yMin));
            Assert.That(layout.BoardRectPixels.yMax, Is.LessThanOrEqualTo(safe.yMax));
        }

        private static void AssertFits(BoardLayout.Result layout, int gridWidth, int gridHeight)
        {
            var tray = layout.BoardRectPixels;
            var space = layout.AvailablePixels;
            const float pixelTolerance = 0.5f;
            Assert.That(tray.xMin, Is.GreaterThanOrEqualTo(space.xMin - pixelTolerance));
            Assert.That(tray.xMax, Is.LessThanOrEqualTo(space.xMax + pixelTolerance));
            Assert.That(tray.yMin, Is.GreaterThanOrEqualTo(space.yMin - pixelTolerance));
            Assert.That(tray.yMax, Is.LessThanOrEqualTo(space.yMax + pixelTolerance));
            Assert.That(tray.center.x, Is.EqualTo(space.center.x).Within(pixelTolerance));
            Assert.That(tray.center.y, Is.EqualTo(space.center.y).Within(pixelTolerance));
            Assert.That(tray.width / (gridWidth + 2f * BoardLayout.PanelPadding),
                Is.EqualTo(tray.height / (gridHeight + 2f * BoardLayout.PanelPadding)).Within(pixelTolerance));
        }
    }
}
