using System;
using UnityEngine;
using UnityEngine.UI;

namespace Plop.Presentation
{
    /// <summary>
    /// Phase 13 animated splash, reproduced natively from the branding pack's PNG layers and Animation_Timeline.json
    /// (no video). One overlay canvas above the main menu, which is already built and live underneath: the splash
    /// never holds the game back. It plays the ~1.35 s timeline once per cold start (squash/stretch drop with a soft
    /// landing, logo pop-in), then fades out and destroys itself, releasing its textures. Any tap skips it.
    /// Reduced Motion shows the rest pose briefly and only fades. No per-frame allocation.
    /// </summary>
    public sealed class BrandSplash : MonoBehaviour
    {
        public const float FadeSeconds = 0.25f;
        public const float ReducedMotionHold = 0.45f;
        private const int SortingOrder = 400; // above the main menu (50) and every HUD canvas

        private SplashTimeline _timeline;
        private RectTransform _mascot, _shadow, _logo;
        private Image _mascotImage, _shadowImage, _logoImage, _bgImage;
        private CanvasGroup _group;
        private Vector2 _mascotRest;
        private float _clock = -1f, _fadeFrom = -1f, _hold = -1f;
        private bool _reduced;
        private Sprite[] _owned = Array.Empty<Sprite>();

        public event Action Finished;
        public bool IsPlaying => this != null && enabled;
        public float Elapsed => Mathf.Max(0f, _clock);
        public SplashTimeline Timeline => _timeline;
        internal RectTransform MascotRect => _mascot;
        internal RectTransform LogoRect => _logo;

        /// <summary>Creates the overlay from the profile's layers. Returns null when the profile has no splash.</summary>
        public static BrandSplash Show(Transform owner, BrandingProfile profile, bool reducedMotion)
        {
            if (profile == null || !profile.HasSplash) return null;
            SplashTimeline timeline;
            try { timeline = SplashTimeline.Parse(profile.splashTimeline.text); }
            catch (FormatException e)
            {
                Debug.LogWarning("[PLOP] Splash skipped: " + e.Message);
                return null;
            }
            return Show(owner, timeline, profile.splashBackground, profile.splashMascot, profile.splashShadow, profile.splashLogo, reducedMotion);
        }

        public static BrandSplash Show(Transform owner, SplashTimeline timeline, Sprite background, Sprite mascot, Sprite shadow, Sprite logo,
            bool reducedMotion, bool ownsSprites = false)
        {
            var go = new GameObject("Brand Splash", typeof(RectTransform));
            go.transform.SetParent(owner, false);
            var splash = go.AddComponent<BrandSplash>();
            splash.Build(timeline, background, mascot, shadow, logo, reducedMotion);
            if (ownsSprites) splash._owned = new[] { background, mascot, shadow, logo };
            return splash;
        }

        private void Build(SplashTimeline timeline, Sprite background, Sprite mascot, Sprite shadow, Sprite logo, bool reducedMotion)
        {
            _timeline = timeline;
            _reduced = reducedMotion;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = timeline.Canvas;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // the whole reference canvas is always visible
            gameObject.AddComponent<GraphicRaycaster>();
            _group = gameObject.AddComponent<CanvasGroup>();
            var root = (RectTransform)transform;

            // Background covers the screen (aspect-fill), centred like the reference canvas.
            var bg = Layer("Splash background", root, background, out _bgImage);
            var bgImage = _bgImage;
            bg.anchorMin = bg.anchorMax = new Vector2(0.5f, 0.5f);
            var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = background.rect.width / Mathf.Max(1f, background.rect.height);
            bgImage.raycastTarget = true; // swallows taps meant for the menu underneath; a tap skips the splash
            bg.gameObject.AddComponent<Button>().onClick.AddListener(Skip);

            // Reference canvas (Y down, origin top-left) → centred Unity coordinates (Y up).
            Vector2 C(Vector2 p) => new Vector2(p.x - timeline.Canvas.x * 0.5f, timeline.Canvas.y * 0.5f - p.y);

            if (shadow != null)
            {
                _shadow = Layer("Splash shadow", root, shadow, out _shadowImage);
                _shadow.sizeDelta = new Vector2(timeline.ShadowWidth, timeline.ShadowWidth * shadow.rect.height / shadow.rect.width);
                _shadow.anchoredPosition = C(timeline.ShadowCenter);
            }

            _mascot = Layer("Splash mascot", root, mascot, out _mascotImage);
            _mascot.pivot = new Vector2(0.5f, 0f); // bottom-centre: squash and stretch stay grounded
            _mascot.sizeDelta = new Vector2(timeline.MascotWidth, timeline.MascotWidth * mascot.rect.height / mascot.rect.width);
            _mascotRest = C(new Vector2(timeline.MascotCenterX, timeline.MascotRestBaseY));
            _mascot.anchoredPosition = _mascotRest;

            _logo = Layer("Splash logo", root, logo, out _logoImage);
            float fit = Mathf.Min(timeline.LogoMax.x / logo.rect.width, timeline.LogoMax.y / logo.rect.height);
            _logo.sizeDelta = new Vector2(logo.rect.width, logo.rect.height) * fit;
            _logo.anchoredPosition = C(timeline.LogoCenter);

            Apply(_reduced ? timeline.Rest : timeline.Sample(0f));
        }

        private static RectTransform Layer(string name, RectTransform parent, Sprite sprite, out Image image)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return rt;
        }

        private void Apply(SplashTimeline.Pose pose)
        {
            _mascot.anchoredPosition = _mascotRest + new Vector2(0f, -pose.YOffset);
            _mascot.localScale = new Vector3(pose.ScaleX, pose.ScaleY, 1f);
            _mascotImage.color = new Color(1f, 1f, 1f, pose.MascotAlpha);
            _logo.localScale = new Vector3(pose.LogoScale, pose.LogoScale, 1f);
            _logoImage.color = new Color(1f, 1f, 1f, pose.LogoAlpha);
            if (_shadow != null)
            {
                // The contact shadow tightens and darkens as the mascot nears the ground (derived, not keyed).
                float height = Mathf.Clamp01(-pose.YOffset / 300f);
                float s = Mathf.Lerp(1f, 0.55f, height) * Mathf.Lerp(1f, pose.ScaleX, 0.5f);
                _shadow.localScale = new Vector3(s, Mathf.Lerp(1f, 0.7f, height), 1f);
                _shadowImage.color = new Color(1f, 1f, 1f, pose.MascotAlpha * Mathf.Lerp(1f, 0.35f, height));
            }
        }

        /// <summary>Starts the fade-out now (tap, navigation away, or the app needs the screen).</summary>
        public void Skip()
        {
            if (_fadeFrom < 0f) _fadeFrom = Mathf.Max(0f, _clock);
        }

        /// <summary>Development review only: freezes the animation at <paramref name="seconds"/> (still captures).</summary>
        public void HoldAt(float seconds)
        {
            _hold = Mathf.Max(0f, seconds);
            _clock = _hold;
            Apply(_reduced ? _timeline.Rest : _timeline.Sample(Mathf.Min(_hold, _timeline.Duration)));
        }

        /// <summary>Advances the splash; called from Update, and directly by tests.</summary>
        public void Step(float dt)
        {
            if (_hold >= 0f && _fadeFrom < 0f) return;
            // The clock starts on the first rendered frame; a long first frame (startup hitch) must not skip the drop.
            _clock = _clock < 0f ? 0f : _clock + Mathf.Min(dt, 1f / 30f);
            float end = _reduced ? ReducedMotionHold : _timeline.Duration;
            if (_fadeFrom < 0f && _clock >= end) _fadeFrom = end;
            if (!_reduced) Apply(_timeline.Sample(Mathf.Min(_clock, _timeline.Duration)));
            if (_fadeFrom < 0f) return;
            float u = Mathf.Clamp01((_clock - _fadeFrom) / FadeSeconds);
            _group.alpha = 1f - u * u * (3f - 2f * u);
            _group.blocksRaycasts = u < 0.5f;
            if (u >= 1f) Finish();
        }

        private void Update() => Step(Time.unscaledDeltaTime);

        // Leaving the app mid-splash: come back to the game, not to a replay of the intro.
        private void OnApplicationPause(bool paused)
        {
            if (paused) Skip();
        }

        private void Finish()
        {
            enabled = false;
            Finished?.Invoke();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            // Splash art is used once per launch: release the textures instead of keeping ~10 MB resident.
            foreach (var image in new[] { _mascotImage, _shadowImage, _logoImage, _bgImage })
            {
                if (image == null) continue;
                var tex = image.sprite != null ? image.sprite.texture : null;
                // The logo texture may be shared with the main menu title: keep it unless this splash created it.
                if (image == _logoImage && _owned.Length == 0) { image.sprite = null; continue; }
                image.sprite = null;
                if (tex == null) continue;
                if (_owned.Length > 0) Destroy(tex); // runtime-created preview layers (development review)
                else Resources.UnloadAsset(tex);
            }
            foreach (var s in _owned) if (s != null) Destroy(s);
        }
    }
}
