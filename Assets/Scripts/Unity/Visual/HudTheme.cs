using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>Shared canvas units for the play screen, modal controls, and their sprite masks.</summary>
    internal static class HudTheme
    {
        internal static readonly Vector2 Resolution = new Vector2(1080, 1920);
        internal const float CanvasMatch = 0.5f;
        internal const float HeaderReserve = 414f;
        internal const float FooterReserve = 256f;
        internal const float ModalMargin = 30f;
        internal const float PillRadius = 58f;
        internal const float CardRadius = 48f;
        internal const float ControlRadius = 38f;
        internal const float PlayTouchHeight = 148f;
        internal const float StatusWidth = 744f;
        internal const float HeaderWidth = 900f;
        internal const float DeckWidth = 690f;
        internal const float FooterRestY = 109f;
        internal const float VolumeTrackRadius = 12f;
        internal const float VolumeHandleSize = 60f;
        internal const float PillShadowSpread = 14f;
        internal const float CardShadowSpread = 26f;
        internal static readonly Vector2 ShadowOffset = new Vector2(0, -6);
        internal const float ButtonShadow = 0.13f;
        internal const float CardShadow = 0.18f;
        internal const float PressedScale = 0.95f;
        internal const float TintDuration = 0.08f;
        internal const float CompletionEntrance = 0.20f;
        internal const float HintCueDiameter = 66f;
        internal const float HintCueHoldSeconds = 1.35f;
        internal const float HintCueFadeSeconds = 0.20f;
        internal const int Display = 76;
        internal const int Heading = 51;
        internal const int Action = 37;
        internal const int Control = 34;
        internal const int Caption = 32;
        internal const int Icon = 45;
        internal const float HeaderButtonHeight = 105f;
        internal const float FooterButtonHeight = 126f;
        internal const float ModalButtonHeight = 86f;
        internal static readonly Color Ink = PlopPalette.Hex(0x633F55);
        internal static readonly Color ActionInk = PlopPalette.Hex(0x805366);
        internal static readonly Color DisabledText = PlopPalette.Hex(0x806A7B);
        internal static readonly Color HintCueOutline = PlopPalette.Hex(0xB88973);
        internal static readonly Color HintCueFill = PlopPalette.Hex(0xFFE5B7);
        internal static readonly Color MoveSurface = PlopPalette.Hex(0xF4DCE9);
        internal static Color HintSurface => Color.Lerp(PlopPalette.ButtonFill, PlopPalette.Honey, 0.18f);
        internal static Color CompletionSurface => Color.Lerp(PlopPalette.ButtonFill, PlopPalette.Mint, 0.30f);
        internal static Color RecoverySurface => Color.Lerp(PlopPalette.ButtonFill, PlopPalette.Berry, 0.55f);
        internal static Color SecondaryText => Color.Lerp(Ink, PlopPalette.MutedText, 0.33f);
    }
}
