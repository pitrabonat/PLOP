#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.IO;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Development builds only (compiled out of release): preview branding alternatives on a real device before the owner
    /// approves one, without shipping them. Alternative layers are pushed to the app's files folder
    /// (<c>persistentDataPath/branding/</c>) by <c>Tools/Android/branding_review.py</c> (or simctl on iOS) and loaded here.
    /// Preferences (debugprefs-style):
    /// <list type="bullet">
    /// <item><c>plop.debug.splash</c>: "" = normal, "off", "selected" (force the shipped splash), or a folder name under
    /// <c>branding/</c> such as "splash-day" / "splash-night".</item>
    /// <item><c>plop.debug.splash.hold</c>: seconds; freezes the splash at that time (for still captures).</item>
    /// <item><c>plop.debug.logo</c>: "" = profile, "text" = text title, or a file under <c>branding/logos/</c>.</item>
    /// </list>
    /// </summary>
    internal static class BrandingReview
    {
        public const string SplashKey = "plop.debug.splash";
        public const string HoldKey = "plop.debug.splash.hold";
        public const string LogoKey = "plop.debug.logo";

        private static string Root => Path.Combine(Application.persistentDataPath, "branding");

        /// <summary>Returns true when a review setting decided the splash (the default splash is then not shown).</summary>
        public static bool Apply(PlopGameController controller, MainMenu menu, bool reducedMotion, ref bool show)
        {
            string logo = PlayerPrefs.GetString(LogoKey, "");
            if (logo == "text") menu.SetLogo(null);
            else if (logo.Length > 0)
            {
                var sprite = LoadSprite(Path.Combine(Root, "logos", logo));
                if (sprite != null) menu.SetLogo(sprite);
            }

            string splash = PlayerPrefs.GetString(SplashKey, "");
            if (splash.Length == 0) return false;
            if (splash == "off") return true;
            BrandSplash s;
            if (splash == "selected") s = BrandSplash.Show(controller.transform, BrandingProfile.Load(), reducedMotion);
            else
            {
                string dir = Path.Combine(Root, splash);
                string timeline = Path.Combine(dir, "Animation_Timeline.json");
                if (!File.Exists(timeline)) { Debug.LogWarning("[PLOP] Branding review: no " + timeline); return true; }
                s = BrandSplash.Show(controller.transform, SplashTimeline.Parse(File.ReadAllText(timeline)),
                    LoadSprite(Path.Combine(dir, "Background_1080x1920.png")), LoadSprite(Path.Combine(dir, "Mascot.png")),
                    LoadSprite(Path.Combine(dir, "Shadow.png")), LoadSprite(Path.Combine(dir, "Logo.png")), reducedMotion, ownsSprites: true);
            }
            float hold = PlayerPrefs.GetFloat(HoldKey, -1f);
            if (s != null && hold >= 0f) s.HoldAt(hold);
            controller.ShowReviewSplash(s);
            Debug.Log("[PLOP] Branding review: splash " + splash + (hold >= 0f ? " held at " + hold + " s" : "") + ", logo " + (logo.Length > 0 ? logo : "profile"));
            return true;
        }

        private static Sprite LoadSprite(string path)
        {
            if (!File.Exists(path)) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = Path.GetFileNameWithoutExtension(path) };
            if (!tex.LoadImage(File.ReadAllBytes(path), true)) { Object.Destroy(tex); return null; }
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
    }
}
#endif
