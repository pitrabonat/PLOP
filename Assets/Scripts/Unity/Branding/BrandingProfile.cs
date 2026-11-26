using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Phase 13 branding selection used at runtime: the one splash variant that ships (its layers and timeline) and the
    /// optional main-menu logo. Written by PLOP → Branding → Apply (BrandingImporter); only what this asset references
    /// is packed into a player, so alternative designs stay out of builds. Lives in
    /// <c>Assets/Art/Branding/Resources/Branding/BrandingProfile.asset</c>.
    /// </summary>
    public sealed class BrandingProfile : ScriptableObject
    {
        public const string Resource = "Branding/BrandingProfile";

        [Tooltip("Names of the selected pack variants (recorded for docs and tests).")]
        public string iconVariant = "";
        public string splashVariant = "";
        public string menuLogoName = "";

        [Header("Splash layers (selected variant)")]
        public Sprite splashBackground;
        public Sprite splashMascot;
        public Sprite splashShadow;
        public Sprite splashLogo;
        public TextAsset splashTimeline;
        [Tooltip("Colour of the native launch screen and of the splash background's top edge.")]
        public Color launchColor = Color.white;

        [Header("Main menu")]
        [Tooltip("Logo shown instead of the text title on the main menu; empty keeps the text title.")]
        public Sprite menuLogo;

        public bool HasSplash => splashBackground != null && splashMascot != null && splashLogo != null && splashTimeline != null;

        public static BrandingProfile Load() => Resources.Load<BrandingProfile>(Resource);
    }
}
