using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>Shared canvas units for the play screen, modal controls, and their sprite masks.</summary>
    internal static class HudTheme
    {
        internal static readonly Vector2 Resolution = new Vector2(1080, 1920);
        internal const float CanvasMatch = 0.5f;
        internal const float HeaderReserve = 326f;
        internal const float FooterReserve = 294f;
        internal const float ModalMargin = 30f;
        internal const float PillRadius = 58f;
        internal const float CardRadius = 50f;
        internal const float PillShadowSpread = 17f;
        internal const float CardShadowSpread = 26f;
        internal static readonly Vector2 ShadowOffset = new Vector2(0, -6);
        internal const float ButtonShadow = 0.13f;
        internal const float CardShadow = 0.18f;
        internal const float PressedScale = 0.95f;
        internal const float TintDuration = 0.08f;
        internal const float CompletionEntrance = 0.20f;
        internal const int Display = 76;
        internal const int Heading = 51;
        internal const int Action = 39;
        internal const int Control = 36;
        internal const int Caption = 33;
        internal const int Icon = 43;
        internal const float HeaderButtonHeight = 86f;
        internal const float FooterButtonHeight = 116f;
        internal const float ModalButtonHeight = 86f;
        internal static Color SecondaryText => Color.Lerp(PlopPalette.Ink, PlopPalette.MutedText, 0.55f);
    }
}
