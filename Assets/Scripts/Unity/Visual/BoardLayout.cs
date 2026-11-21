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
        public static readonly Vector2 ReferenceResolution = HudTheme.Resolution;
        public const float HeaderHeight = HudTheme.HeaderReserve;     // reference units below the safe-area top
        public const float FooterHeight = HudTheme.FooterReserve;     // reference units above the safe-area bottom
        public const float SideMarginFraction = 0.0625f; // each side of the usable safe width
        public const float PanelPadding = 0.30f;    // world units of container around the grid

        public struct Result
        {
            public float OrthographicSize;
            public Vector2 CameraPosition;   // world XY of the camera (board is centered at world origin)
            public float PixelsPerUnit;      // on-screen pixels per grid cell
            public Rect BoardRectPixels;     // where the board container lands on screen
            public Rect AvailablePixels;
            public Rect SafePixels;
            public float CanvasScale;
            public float ReserveScale;
        }

        /// <summary>Same scale factor CanvasScaler computes for ScaleWithScreenSize, match = 0.5.</summary>
        public static float CanvasScale(float screenW, float screenH)
        {
            float logW = Mathf.Log(screenW / ReferenceResolution.x, 2f);
            float logH = Mathf.Log(screenH / ReferenceResolution.y, 2f);
            return Mathf.Pow(2f, Mathf.Lerp(logW, logH, HudTheme.CanvasMatch));
        }

        public static Result Compute(float screenW, float screenH, Rect safeArea, int gridW, int gridH)
        {
            if (safeArea.width <= 0 || safeArea.height <= 0) safeArea = new Rect(0, 0, screenW, screenH);
            float scale = CanvasScale(screenW, screenH);
            float margin = safeArea.width * SideMarginFraction;
            float topReserve = HeaderHeight * scale;
            float bottomReserve = FooterHeight * scale;
            // Keep a playable height in shallow editor windows without changing the portrait layout.
            float reserveScale = Mathf.Min(1f, safeArea.height * 0.49f / (topReserve + bottomReserve));

            var available = Rect.MinMaxRect(
                safeArea.xMin + margin,
                safeArea.yMin + bottomReserve * reserveScale,
                safeArea.xMax - margin,
                safeArea.yMax - topReserve * reserveScale);

            float boardW = gridW + 2 * PanelPadding, boardH = gridH + 2 * PanelPadding;
            float ppu = Mathf.Min(available.width / boardW, available.height / boardH);

            var r = new Result
            {
                PixelsPerUnit = ppu,
                OrthographicSize = screenH / (2f * ppu),
                AvailablePixels = available,
                SafePixels = safeArea,
                CanvasScale = scale,
                ReserveScale = reserveScale
            };
            // Put world (0,0) — the board center — at the center of the available region.
            Vector2 center = available.center;
            r.CameraPosition = new Vector2((screenW * 0.5f - center.x) / ppu, (screenH * 0.5f - center.y) / ppu);
            r.BoardRectPixels = new Rect(center.x - boardW * ppu * 0.5f, center.y - boardH * ppu * 0.5f, boardW * ppu, boardH * ppu);
            return r;
        }
    }
}
