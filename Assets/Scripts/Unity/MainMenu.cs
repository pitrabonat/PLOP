using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Plop.Presentation
{
    /// <summary>
    /// Portrait main menu in Soft Toy Minimalism, built in code (uGUI) above the gameplay HUD.
    /// Hierarchy: logo → sleepy jelly Plops → large PLAY → Daily Gift · Daily Puzzle → Levels · Studio → Settings.
    /// The mascots are real Plop rigs wearing the equipped look; they animate only while the menu is visible
    /// and react when tapped.
    /// </summary>
    public sealed partial class MainMenu : MonoBehaviour
    {
        public event Action PlayClicked, DailyGiftClicked, DailyPuzzleClicked, LevelsClicked, SettingsClicked, StudioClicked;

        private const float FadeSeconds = 0.22f;
        private static readonly Vector2[] MascotGround = { new Vector2(-220f, -112f), new Vector2(0f, -112f), new Vector2(220f, -112f) };
        private static readonly float[] MascotScale = { 250f, 300f, 250f };
        private static readonly int[] MascotColors = { 0, 1, 2 };
        private static readonly Reaction[] TapReactions = { Reaction.Bounce, Reaction.HappyBlink, Reaction.Wobble, Reaction.Squash, Reaction.Surprised };

        private MenuKit _kit;
        private RectTransform _root, _safe, _mascots, _mascotRigs;
        private CanvasGroup _group;
        private Text _dust, _playSubtitle, _giftLabel, _dailyLabel, _toast;
        private Image _giftDot, _dailyCheck, _studioDot;
        private Button _giftButton;
        private readonly PlopView[] _plops = new PlopView[3];
        private CharacterLook _look;
        private Coroutine _fade, _toastAnim;
        private bool _reducedMotion;
        private int _tapCount;
        private float _nextTap;

        /// <summary>True while the menu covers the board and takes input (false once a fade-out has begun).</summary>
        public bool IsOpen => _root != null && _root.gameObject.activeSelf && _group.blocksRaycasts;

        public bool ReducedMotion
        {
            set
            {
                _reducedMotion = value;
                foreach (var p in _plops) p?.SetReducedMotion(value);
            }
        }

        internal PlopView Mascot(int i) => _plops[i];
        internal CharacterLook Look => _look;

        private void Awake()
        {
            _kit = new MenuKit();
            _root = MenuKit.Canvas(transform, "Main Menu Canvas", 50, out _group); // above the gameplay HUD
            var background = MenuKit.Rect("Background", _root);
            background.gameObject.AddComponent<Image>().color = PlopPalette.PlayBackground;
            BuildAtmosphere();
            _safe = MenuKit.Rect("SafeArea", _root);
            MenuKit.FitSafeArea(_safe);

            // ---- top bar: Settings (left), Dream Dust balance (right) ----
            var settings = _kit.Pill(_safe, "Menu Settings", new Vector2(0f, 1f), new Vector2(110, -100), new Vector2(124, 116),
                PlopPalette.ButtonFill, null, HudIcons.Kind.Settings, () => SettingsClicked?.Invoke());
            settings.GetComponentInChildren<Text>()?.gameObject.SetActive(false);

            var dustPill = MenuKit.Card(_safe, "Dream Dust", new Vector2(1f, 1f), new Vector2(-150, -100), new Vector2(236, 96), PlopPalette.ButtonFill);
            MenuKit.Glyph(dustPill, HudIcons.Kind.Star, new Vector2(-66, 0), 54, PlopPalette.Honey);
            _dust = _kit.Label(dustPill, "Dust amount", HudTheme.Action, FontStyle.Bold, HudTheme.Ink, new Vector2(24, 0), new Vector2(140, 70));

            // ---- logo ----
            var logo = _kit.Label(_safe, "Logo", 196, FontStyle.Bold, HudTheme.Ink, new Vector2(0, 560), new Vector2(900, 230), _kit.Display);
            logo.text = "PLOP!";
            _logoText = logo;
            var tagline = _kit.Label(_safe, "Tagline", HudTheme.Caption + 4, FontStyle.Bold, HudTheme.SecondaryText, new Vector2(0, 436), new Vector2(700, 60));
            Loc.Set(tagline, "Sleepy Puzzle");

            // ---- jelly Plops on a pillow (tap them) ----
            _mascots = MenuKit.Rect("Mascots", _safe);
            _mascots.anchorMin = _mascots.anchorMax = new Vector2(0.5f, 0.5f);
            _mascots.sizeDelta = new Vector2(760, 330);
            _mascots.anchoredPosition = new Vector2(0, 170);
            var pillow = MenuKit.Card(_mascots, "Pillow", new Vector2(0.5f, 0.5f), new Vector2(0, -110), new Vector2(640, 110),
                Color.Lerp(PlopPalette.TrayRim, PlopPalette.Berry, 0.18f));
            pillow.SetAsFirstSibling();
            var tap = MenuKit.Rect("Mascot tap area", _mascots);
            tap.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            tap.gameObject.AddComponent<Button>().onClick.AddListener(PokeMascots);
            SetLook(CharacterLook.Default);

            // ---- PLAY ----
            var play = _kit.Pill(_safe, "Play", new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(600, 176),
                Color.Lerp(PlopPalette.Berry, PlopPalette.ButtonFill, 0.12f), "Play", HudIcons.Kind.Play, () => PlayClicked?.Invoke(), 64);
            _playSubtitle = _kit.Label(play, "Play level", HudTheme.Caption, FontStyle.Bold, HudTheme.ActionInk, new Vector2(40, -52), new Vector2(360, 44));
            var playLabel = play.Find("Label");
            if (playLabel != null) ((RectTransform)playLabel).anchoredPosition = new Vector2(40, 14);

            // ---- Daily Gift · Daily Puzzle ----
            _giftButton = _kit.Tile(_safe, "Daily Gift", new Vector2(-165, -420), HudIcons.Kind.Gift, PlopPalette.Honey, () => DailyGiftClicked?.Invoke(), out _giftLabel);
            _giftDot = MenuKit.Glyph((RectTransform)_giftButton.transform, HudIcons.Kind.Star, new Vector2(108, 64), 40, PlopPalette.Berry);
            _giftDot.name = "Gift available";
            var daily = _kit.Tile(_safe, "Daily Puzzle", new Vector2(165, -420), HudIcons.Kind.Calendar, PlopPalette.Mint, () => DailyPuzzleClicked?.Invoke(), out _dailyLabel);
            _dailyCheck = MenuKit.Glyph((RectTransform)daily.transform, HudIcons.Kind.Check, new Vector2(108, 64), 44, Color.Lerp(PlopPalette.Mint, PlopPalette.Ink, 0.45f));
            _dailyCheck.name = "Daily done";

            // ---- Levels · Studio (both smaller than PLAY) ----
            _kit.Pill(_safe, "Levels", new Vector2(0.5f, 0.5f), new Vector2(-165, -640), new Vector2(300, 116), PlopPalette.ButtonFill,
                "Levels", HudIcons.Kind.Levels, () => LevelsClicked?.Invoke());
            var studio = _kit.Pill(_safe, "Studio", new Vector2(0.5f, 0.5f), new Vector2(165, -640), new Vector2(300, 116),
                Color.Lerp(PlopPalette.ButtonFill, PlopPalette.Lavender, 0.22f), "Studio", HudIcons.Kind.Studio, () => StudioClicked?.Invoke());
            _studioDot = MenuKit.Glyph(studio, HudIcons.Kind.Star, new Vector2(128, 44), 34, PlopPalette.Berry);
            _studioDot.name = "Studio news";
            _studioDot.gameObject.SetActive(false);

            BuildBonusOffer();

            _toast = _kit.Label(_safe, "Toast", HudTheme.Action, FontStyle.Bold, HudTheme.Ink, new Vector2(0, -300), new Vector2(900, 60));
            _toast.gameObject.SetActive(false);
            _root.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ API

        private Text _logoText;
        private Image _logoArt;
        internal Image LogoArt => _logoArt;

        /// <summary>
        /// Phase 13 branding: shows <paramref name="logo"/> instead of the text title, fitted (aspect kept) into the
        /// title's box above the tagline. Null restores the text title. Nothing else on the menu moves.
        /// </summary>
        public void SetLogo(Sprite logo)
        {
            if (_logoText == null) return;
            if (logo == null)
            {
                if (_logoArt != null) _logoArt.gameObject.SetActive(false);
                _logoText.gameObject.SetActive(true);
                return;
            }
            if (_logoArt == null)
            {
                var rt = MenuKit.Rect("Logo art", _safe);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                _logoArt = rt.gameObject.AddComponent<Image>();
                _logoArt.raycastTarget = false;
            }
            // Illustrated logos carry splash drops above the letters, so they need more height than the text title
            // to read at the same size. The box ends just above the tagline and stays clear of the top bar.
            var box = new Vector2(800f, 380f);
            float fit = Mathf.Min(box.x / logo.rect.width, box.y / logo.rect.height);
            var art = (RectTransform)_logoArt.transform;
            art.sizeDelta = new Vector2(logo.rect.width, logo.rect.height) * fit;
            art.anchoredPosition = new Vector2(0f, 670f);
            _logoArt.sprite = logo;
            _logoArt.gameObject.SetActive(true);
            _logoText.gameObject.SetActive(false);
        }

        public void Refresh(int dust, int resumeLevelNumber, bool giftAvailable, bool dailyDone, bool dailyPerfect)
        {
            _dust.text = dust.ToString();
            Loc.Set(_playSubtitle, "Level {0}", resumeLevelNumber);
            _giftDot.gameObject.SetActive(giftAvailable);
            Loc.Set(_giftLabel, giftAvailable ? "Gift" : "Tomorrow");
            _giftButton.interactable = true;
            _dailyCheck.gameObject.SetActive(dailyDone);
            _dailyCheck.sprite = HudIcons.Get(dailyPerfect ? HudIcons.Kind.Star : HudIcons.Kind.Check);
            Loc.Set(_dailyLabel, "Daily");
        }

        /// <summary>Shows a small dot on Studio when a milestone unlocked something new.</summary>
        public void SetStudioNews(bool news) => _studioDot.gameObject.SetActive(news);

        /// <summary>Re-dresses the mascots in the equipped look.</summary>
        public void SetLook(CharacterLook look)
        {
            look = look ?? CharacterLook.Default;
            if (_look != null && _look.Key == look.Key) return;
            _look = look;
            if (_mascotRigs != null) Destroy(_mascotRigs.gameObject);
            _mascotRigs = MenuKit.Rect("Mascot rigs", _mascots);
            _mascotRigs.SetSiblingIndex(1); // above the pillow, below the tap area
            var atlas = ShapeAtlas.ForDensity(256, look);
            for (int i = 0; i < 3; i++)
            {
                var view = new PlopView(_mascotRigs, atlas, MascotColors[i], 11 + i * 7, _reducedMotion, ui: true);
                view.Root.localScale = new Vector3(MascotScale[i], MascotScale[i], 1f);
                // The root is a cell centre; the jelly's ground sits 0.345 cells below it.
                view.Snap(new Vector3(MascotGround[i].x, MascotGround[i].y + 0.345f * MascotScale[i], 0f), false);
                if (i == 1) view.SetExpression(Expression.Sleeping); // the middle one dozes, eyes closed
                _plops[i] = view;
            }
        }

        public void ShowToast(string message)
        {
            Loc.SetRaw(_toast, message);
            _toast.gameObject.SetActive(true);
            if (_toastAnim != null) StopCoroutine(_toastAnim);
            _toastAnim = StartCoroutine(HideToast());
        }

        public void Show()
        {
            if (_fade != null) StopCoroutine(_fade);
            _root.gameObject.SetActive(true);
            _group.blocksRaycasts = true;
            MenuKit.FitSafeArea(_safe);
            if (_reducedMotion) { _group.alpha = 1f; return; }
            _fade = StartCoroutine(Fade(0f, 1f, false));
        }

        public void Hide(bool instant = false)
        {
            if (!_root.gameObject.activeSelf) return;
            if (_fade != null) StopCoroutine(_fade);
            _group.blocksRaycasts = false;
            if (instant || _reducedMotion) { _root.gameObject.SetActive(false); return; }
            _fade = StartCoroutine(Fade(1f, 0f, true));
        }

        private void PokeMascots()
        {
            float now = Time.unscaledTime;
            if (now < _nextTap) return; // short cooldown: rapid taps never stack animations
            _nextTap = now + 0.45f;
            int who = _tapCount % 3 == 0 ? 0 : _tapCount % 3 == 1 ? 2 : 1;
            var reaction = who == 1 ? Reaction.HappyBlink : TapReactions[(_tapCount / 3 + who) % TapReactions.Length];
            _tapCount++;
            _plops[who]?.React(reaction, now);
        }

        // ------------------------------------------------------------------ animation

        private const int MoteCount = 14;
        private readonly RectTransform[] _motes = new RectTransform[MoteCount];
        private readonly Image[] _moteImages = new Image[MoteCount];
        private readonly Vector4[] _moteMotion = new Vector4[MoteCount]; // x0, speed, phase, base alpha
        private static Sprite _moteSprite, _skySprite;

        /// <summary>
        /// Sprint 02 menu atmosphere: a soft dawn gradient over the flat background and a few "dream dust" motes that drift
        /// upward and twinkle (static under Reduced Motion). Behind all controls; nothing in the layout moves.
        /// </summary>
        private void BuildAtmosphere()
        {
            var sky = MenuKit.Rect("Menu sky", _root);
            var skyImage = sky.gameObject.AddComponent<Image>();
            skyImage.sprite = _skySprite != null ? _skySprite : (_skySprite = SkyGradient());
            skyImage.raycastTarget = false;

            var layer = MenuKit.Rect("Dream motes", _root);
            var random = new System.Random(20261127);
            for (int i = 0; i < MoteCount; i++)
            {
                var mote = new GameObject("Mote", typeof(RectTransform)).GetComponent<RectTransform>();
                mote.SetParent(layer, false);
                float x = (float)random.NextDouble(), y = (float)random.NextDouble();
                mote.anchorMin = mote.anchorMax = new Vector2(x, y);
                float size = 10f + 16f * (float)random.NextDouble();
                mote.sizeDelta = new Vector2(size, size);
                var image = mote.gameObject.AddComponent<Image>();
                image.sprite = _moteSprite != null ? _moteSprite : (_moteSprite = SoftDot());
                image.raycastTarget = false;
                image.color = Color.Lerp(PlopPalette.Honey, Color.white, i % 3 == 0 ? 0.65f : 0.25f);
                _motes[i] = mote;
                _moteImages[i] = image;
                _moteMotion[i] = new Vector4(x, 0.006f + 0.010f * (float)random.NextDouble(),
                    (float)(random.NextDouble() * Mathf.PI * 2), 0.30f + 0.35f * (float)random.NextDouble());
                SetMoteAlpha(i, 0f);
            }
        }

        private void SetMoteAlpha(int i, float time)
        {
            var m = _moteMotion[i];
            float twinkle = _reducedMotion ? 1f : 0.65f + 0.35f * Mathf.Sin(time * 1.3f + m.z);
            var c = _moteImages[i].color;
            c.a = m.w * twinkle;
            _moteImages[i].color = c;
        }

        private void TickAtmosphere(float now, float dt)
        {
            if (_motes[0] == null) return;
            for (int i = 0; i < MoteCount; i++)
            {
                SetMoteAlpha(i, now);
                if (_reducedMotion) continue;
                var m = _moteMotion[i];
                var a = _motes[i].anchorMin;
                a.y += m.y * dt;
                if (a.y > 1.04f) a.y = -0.04f;
                a.x = m.x + 0.012f * Mathf.Sin(now * 0.4f + m.z);
                _motes[i].anchorMin = _motes[i].anchorMax = a;
            }
        }

        private static Sprite SkyGradient()
        {
            const int h = 64;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var top = Color.Lerp(PlopPalette.PlayBackground, Color.white, 0.45f);
            var bottom = Color.Lerp(PlopPalette.PlayBackground, PlopPalette.Honey, 0.16f);
            for (int y = 0; y < h; y++)
            {
                float t = y / (h - 1f);
                tex.SetPixel(0, y, Color.Lerp(bottom, top, t * t * (3f - 2f * t)));
            }
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite SoftDot()
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        private void Update()
        {
            if (_root == null || !_root.gameObject.activeSelf) return;
            float now = Time.unscaledTime;
            TickAtmosphere(now, Time.unscaledDeltaTime);
            for (int i = 0; i < _plops.Length; i++)
            {
                var p = _plops[i];
                if (p == null) continue;
                p.Tick(now);
                if (i == 1 && p.CurrentExpression == Expression.Neutral) p.SetExpression(Expression.Sleeping);
            }
        }

        private IEnumerator Fade(float from, float to, bool deactivate)
        {
            for (float u = 0f; u < 1f; u += Time.unscaledDeltaTime / FadeSeconds)
            {
                _group.alpha = Mathf.Lerp(from, to, u * u * (3f - 2f * u));
                yield return null;
            }
            _group.alpha = to;
            if (deactivate) _root.gameObject.SetActive(false);
            _fade = null;
        }

        private IEnumerator HideToast()
        {
            yield return new WaitForSecondsRealtime(1.6f);
            _toast.gameObject.SetActive(false);
            _toastAnim = null;
        }
    }
}
