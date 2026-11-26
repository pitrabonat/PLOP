using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Plop.Core;
using Plop.Solver;
using UnityEngine;
using UnityEngine.TestTools;
using Plop.Presentation;

namespace Plop.Tests.PlayMode
{
    /// <summary>
    /// Phase 13 branding: the pack's splash timeline is reproduced natively (no video), never holds the game back,
    /// can be skipped, respects Reduced Motion and frees its art; the runtime profile carries only the selected
    /// variant; the main-menu logo replaces the text title without moving anything else.
    /// </summary>
    public class Phase13BrandingTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
        }

        private static LevelDefinition Level()
        {
            var l = LevelText.Parse("one", "R..r");
            return l.WithOptimalMoves(BfsSolver.Solve(l).SolutionLength);
        }

        private static BrandingProfile Profile()
        {
            var p = BrandingProfile.Load();
            Assert.IsNotNull(p, "Resources/" + BrandingProfile.Resource);
            return p;
        }

        private BrandSplash Splash(bool reduced)
        {
            var owner = new GameObject("Splash owner");
            _made.Add(owner);
            var splash = BrandSplash.Show(owner.transform, Profile(), reduced);
            Assert.IsNotNull(splash);
            return splash;
        }

        // ------------------------------------------------------------------ timeline

        [Test]
        public void Timeline_MatchesThePackKeyframes()
        {
            var t = SplashTimeline.Parse(Profile().splashTimeline.text);
            Assert.AreEqual(7, t.KeyCount);
            Assert.AreEqual(1.35f, t.Duration, 1e-4f);
            Assert.AreEqual(new Vector2(1080f, 1920f), t.Canvas);
            Assert.AreEqual(540f, t.MascotWidth);
            Assert.AreEqual(1220f, t.MascotRestBaseY);
            Assert.AreEqual(new Vector2(540f, 680f), t.LogoCenter);

            var start = t.Sample(0f);
            Assert.AreEqual(-300f, start.YOffset, 1e-3f);
            Assert.AreEqual(0f, start.MascotAlpha, 1e-3f);
            Assert.AreEqual(0f, start.LogoAlpha, 1e-3f);
            var impact = t.Sample(0.39f);
            Assert.AreEqual(0f, impact.YOffset, 1e-3f);
            Assert.AreEqual(1.18f, impact.ScaleX, 1e-3f, "squash at first contact");
            Assert.AreEqual(0.76f, impact.ScaleY, 1e-3f);
            var rest = t.Sample(5f);
            Assert.AreEqual(1f, rest.ScaleX, 1e-4f);
            Assert.AreEqual(1f, rest.ScaleY, 1e-4f);
            Assert.AreEqual(1f, rest.LogoAlpha, 1e-4f);
            Assert.AreEqual(1f, rest.LogoScale, 1e-4f);
            Assert.AreEqual(start.YOffset, t.Sample(-1f).YOffset, "clamped before the start");
        }

        [Test]
        public void Timeline_InterpolatesWithSmoothstep()
        {
            var t = SplashTimeline.Parse(Profile().splashTimeline.text);
            // Between t=0 (y −300) and t=0.22 (y −145): a quarter of the way is smoothstep(0.25) = 0.15625.
            float y = t.Sample(0.055f).YOffset;
            Assert.AreEqual(Mathf.Lerp(-300f, -145f, 0.15625f), y, 0.05f);
            Assert.AreEqual(Mathf.Lerp(-300f, -145f, 0.5f), t.Sample(0.11f).YOffset, 0.05f, "midpoint");
        }

        [Test]
        public void Timeline_WithoutKeyframesIsRejected()
        {
            Assert.Throws<System.FormatException>(() => SplashTimeline.Parse("{\"duration_seconds\": 1, \"keyframes\": []}"));
        }

        // ------------------------------------------------------------------ splash behaviour

        [Test]
        public void Splash_PlaysOnceFadesAndDestroysItself()
        {
            var splash = Splash(reduced: false);
            bool finished = false;
            splash.Finished += () => finished = true;
            var canvas = splash.GetComponent<Canvas>();
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.Greater(canvas.sortingOrder, 100, "above the menu and HUD");

            splash.Step(0f); // first rendered frame starts the clock
            float total = 0f;
            int frames = 0;
            while (!finished && frames < 1000) { splash.Step(1f / 60f); total += 1f / 60f; frames++; }
            Assert.IsTrue(finished);
            Assert.AreEqual(splash.Timeline.Duration + BrandSplash.FadeSeconds, total, 0.05f, "≈1.6 s, never longer");
        }

        [Test]
        public void Splash_FirstLongFrameDoesNotSkipTheDrop()
        {
            var splash = Splash(reduced: false);
            splash.Step(2.5f); // a startup hitch
            Assert.AreEqual(0f, splash.Elapsed, 1e-4f);
            splash.Step(2.5f);
            Assert.LessOrEqual(splash.Elapsed, 1f / 30f + 1e-4f, "dt is clamped so the animation is seen, not jumped");
        }

        [Test]
        public void Splash_SquashesFromABottomCentrePivot()
        {
            var splash = Splash(reduced: false);
            Assert.AreEqual(new Vector2(0.5f, 0f), splash.MascotRect.pivot);
            splash.Step(0f);
            for (int i = 0; i < 23; i++) splash.Step(1f / 60f); // ≈0.383 s, near first contact
            Assert.Greater(splash.MascotRect.localScale.x, 1.1f);
            Assert.Less(splash.MascotRect.localScale.y, 0.85f);
        }

        [Test]
        public void Splash_TapSkipsWithAShortFade()
        {
            var splash = Splash(reduced: false);
            bool finished = false;
            splash.Finished += () => finished = true;
            splash.Step(0f);
            splash.Step(0.1f);
            splash.Skip();
            int frames = 0;
            while (!finished && frames < 100) { splash.Step(1f / 60f); frames++; }
            Assert.IsTrue(finished);
            Assert.LessOrEqual(frames, Mathf.CeilToInt(BrandSplash.FadeSeconds * 60f) + 2);
        }

        [Test]
        public void Splash_ReducedMotionShowsTheRestPoseBriefly()
        {
            var splash = Splash(reduced: true);
            bool finished = false;
            splash.Finished += () => finished = true;
            Assert.AreEqual(Vector3.one, splash.MascotRect.localScale, "no squash");
            Assert.AreEqual(1f, splash.LogoRect.localScale.x, 1e-4f);
            splash.Step(0f);
            float total = 0f;
            int frames = 0;
            while (!finished && frames < 1000)
            {
                splash.Step(1f / 60f);
                total += 1f / 60f;
                frames++;
                if (!finished) Assert.AreEqual(Vector3.one, splash.MascotRect.localScale);
            }
            Assert.IsTrue(finished);
            Assert.AreEqual(BrandSplash.ReducedMotionHold + BrandSplash.FadeSeconds, total, 0.05f, "shorter than the full animation");
        }

        [UnityTest]
        public IEnumerator Splash_IsRemovedAfterFinishing()
        {
            var splash = Splash(reduced: true);
            var go = splash.gameObject;
            splash.Step(0f);
            for (int i = 0; i < 120; i++) splash.Step(1f / 60f);
            yield return null;
            Assert.IsTrue(go == null, "destroyed, its textures released");
        }

        // ------------------------------------------------------------------ profile and menu

        [Test]
        public void Profile_ReferencesOnlyTheSelectedVariant()
        {
            var p = Profile();
            Assert.IsTrue(p.HasSplash);
            CollectionAssert.Contains(new[] { "Day", "Night" }, p.splashVariant);
            CollectionAssert.Contains(new[] { "Hero_Pink", "Lilac", "Mint" }, p.iconVariant);
            Assert.AreEqual(1080, p.splashBackground.texture.width);
            Assert.AreEqual(1920, p.splashBackground.texture.height);
            Assert.AreEqual(1f, p.launchColor.a);
            if (p.menuLogoName == "text") Assert.IsNull(p.menuLogo);
            else Assert.AreEqual(p.menuLogoName, p.menuLogo.name);
        }

        [UnityTest]
        public IEnumerator MenuLogo_ReplacesTheTitleAndRestoresIt()
        {
            var root = new GameObject("Branding menu test");
            _made.Add(root);
            var controller = root.AddComponent<PlopGameController>();
            controller.RememberProgress = false;
            controller.UseLevels(new List<LevelDefinition> { Level() });
            yield return null;
            var menu = controller.Menu;
            var logo = Profile().menuLogo ?? Profile().splashLogo;
            menu.SetLogo(logo);
            Assert.IsTrue(menu.LogoArt.gameObject.activeSelf);
            var size = ((RectTransform)menu.LogoArt.transform).sizeDelta;
            Assert.LessOrEqual(size.x, 680.5f);
            Assert.LessOrEqual(size.y, 250.5f);
            Assert.AreEqual(logo.rect.width / logo.rect.height, size.x / size.y, 0.01f, "aspect kept");
            menu.SetLogo(null);
            Assert.IsFalse(menu.LogoArt.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator TestCatalogs_NeverShowTheSplash_AndBackSkipsIt()
        {
            var root = new GameObject("Branding controller test");
            _made.Add(root);
            var controller = root.AddComponent<PlopGameController>();
            controller.RememberProgress = false;
            controller.UseLevels(new List<LevelDefinition> { Level() });
            yield return null;
            Assert.IsNull(controller.Splash, "no splash for tests/tools");

            controller.ShowReviewSplash(BrandSplash.Show(root.transform, Profile(), false));
            Assert.IsNotNull(controller.Splash);
            Assert.AreEqual("splash", controller.HandleBack());
            float until = Time.realtimeSinceStartup + 2f;
            while (controller.Splash != null && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsNull(controller.Splash, "Back fades the splash out quickly");
        }
    }
}
