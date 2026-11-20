using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Portrait layout math (pure, testable). Fits the board — including its rounded container — into the
    /// part of the safe area that is not used by the header and the bottom controls, keeps square cells,
    /// and centers the board in that region. UI sizes are in canvas reference units (1080×1920, match 0.5),
    /// converted with the same formula as Unity's CanvasScaler.
    /// </summary>
    public static class BoardLayout
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1080, 1920);
        public const float HeaderHeight = 230f;     // reference units below the safe-area top
        public const float FooterHeight = 270f;     // reference units above the safe-area bottom
        public const float SideMarginFraction = 0.045f; // of screen width (rounded display corners)
        public const float MinVirtualCells = 5.2f;  // small boards are not blown up beyond ~5.2 cells across
        public const float PanelPadding = 0.30f;    // world units of container around the grid

        public struct Result
        {
            public float OrthographicSize;
            public Vector2 CameraPosition;   // world XY of the camera (board is centered at world origin)
            public float PixelsPerUnit;      // on-screen pixels per grid cell
            public Rect BoardRectPixels;     // where the board container lands on screen
            public Rect AvailablePixels;
            public float CanvasScale;
        }

        /// <summary>Same scale factor CanvasScaler computes for ScaleWithScreenSize, match = 0.5.</summary>
        public static float CanvasScale(float screenW, float screenH)
        {
            float logW = Mathf.Log(screenW / ReferenceResolution.x, 2f);
            float logH = Mathf.Log(screenH / ReferenceResolution.y, 2f);
            return Mathf.Pow(2f, Mathf.Lerp(logW, logH, 0.5f));
        }

        public static Result Compute(float screenW, float screenH, Rect safeArea, int gridW, int gridH)
        {
            if (safeArea.width <= 0 || safeArea.height <= 0) safeArea = new Rect(0, 0, screenW, screenH);
            float scale = CanvasScale(screenW, screenH);
            float margin = Mathf.Max(screenW * SideMarginFraction, safeArea.xMin, screenW - safeArea.xMax);

            var available = Rect.MinMaxRect(
                Mathf.Max(safeArea.xMin, margin),
                safeArea.yMin + FooterHeight * scale,
                Mathf.Min(safeArea.xMax, screenW - margin),
                safeArea.yMax - HeaderHeight * scale);
            if (available.width < 10f || available.height < 10f)
                available = new Rect(margin, 0, Mathf.Max(10f, screenW - 2 * margin), screenH);

            float boardW = gridW + 2 * PanelPadding, boardH = gridH + 2 * PanelPadding;
            float fitW = Mathf.Max(boardW, MinVirtualCells), fitH = Mathf.Max(boardH, MinVirtualCells);
            float ppu = Mathf.Min(available.width / fitW, available.height / fitH);

            var r = new Result
            {
                PixelsPerUnit = ppu,
                OrthographicSize = screenH / (2f * ppu),
                AvailablePixels = available,
                CanvasScale = scale
            };
            // Put world (0,0) — the board center — at the center of the available region.
            Vector2 center = available.center;
            r.CameraPosition = new Vector2((screenW * 0.5f - center.x) / ppu, (screenH * 0.5f - center.y) / ppu);
            r.BoardRectPixels = new Rect(center.x - boardW * ppu * 0.5f, center.y - boardH * ppu * 0.5f, boardW * ppu, boardH * ppu);
            return r;
        }
    }
}
