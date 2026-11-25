using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Plop.Presentation;
using UnityEngine;

namespace Plop.Tests.PlayMode
{
    /// <summary>
    /// Phase 11: every board size that ships (campaign and Daily) fits inside the safe area on small, tall,
    /// notched and tablet screens, with cells large enough to read and swipe.
    /// </summary>
    public sealed class Phase11LayoutTests
    {
        // width, height, safe bottom inset, safe top inset (pixels), pixels per point/dp; names for messages only.
        private static readonly (string Name, int W, int H, int Bottom, int Top, float Scale)[] Screens =
        {
            ("iPhone SE", 750, 1334, 0, 40, 2f),
            ("iPhone 15 Pro Max", 1290, 2796, 102, 177, 3f),
            ("Pixel 7", 1080, 2400, 0, 118, 2.625f),
            ("small Android", 720, 1280, 0, 48, 2f),
            ("tall Android", 1080, 2520, 63, 136, 2.625f),
            ("iPad", 1536, 2048, 40, 48, 2f),
        };

        [Test]
        public void EveryShippedBoardFitsEveryScreen()
        {
            var sizes = new HashSet<(int, int)>();
            foreach (var l in LevelLibrary.Load(LevelLibrary.DefaultPack).Concat(LevelLibrary.Load(LevelLibrary.DailyPack)))
                sizes.Add((l.Width, l.Height));
            Assert.Greater(sizes.Count, 2);
            foreach (var s in Screens)
            {
                var safe = new Rect(0, s.Bottom, s.W, s.H - s.Bottom - s.Top);
                float points = s.Scale;
                foreach (var (w, h) in sizes)
                {
                    var layout = BoardLayout.Compute(s.W, s.H, safe, w, h);
                    string label = s.Name + " " + w + "×" + h;
                    Assert.GreaterOrEqual(layout.BoardRectPixels.xMin, safe.xMin - 0.5f, label);
                    Assert.LessOrEqual(layout.BoardRectPixels.xMax, safe.xMax + 0.5f, label);
                    Assert.GreaterOrEqual(layout.BoardRectPixels.yMin, safe.yMin - 0.5f, label);
                    Assert.LessOrEqual(layout.BoardRectPixels.yMax, safe.yMax + 0.5f, label);
                    Assert.GreaterOrEqual(layout.PixelsPerUnit / points, 44f, label + ": a cell is at least 44 pt");
                }
            }
        }
    }
}
