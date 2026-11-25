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
    public sealed class MainMenu : MonoBehaviour
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
            var tagline = _kit.Label(_safe, "Tagline", HudTheme.Caption + 4, FontStyle.Bold, HudTheme.SecondaryText, new Vector2(0, 436), new Vector2(700, 60));
            tagline.text = "Sleepy Puzzle";

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

            _toast = _kit.Label(_safe, "Toast", HudTheme.Action, FontStyle.Bold, HudTheme.Ink, new Vector2(0, -300), new Vector2(900, 60));
            _toast.gameObject.SetActive(false);
            _root.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ API

        public void Refresh(int dust, int resumeLevelNumber, bool giftAvailable, bool dailyDone, bool dailyPerfect)
        {
            _dust.text = dust.ToString();
            _playSubtitle.text = "Level " + resumeLevelNumber;
            _giftDot.gameObject.SetActive(giftAvailable);
            _giftLabel.text = giftAvailable ? "Gift" : "Tomorrow";
            _giftButton.interactable = true;
            _dailyCheck.gameObject.SetActive(dailyDone);
            _dailyCheck.sprite = HudIcons.Get(dailyPerfect ? HudIcons.Kind.Star : HudIcons.Kind.Check);
            _dailyLabel.text = "Daily";
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
                if (i == 1) view.SetExpression(Expression.Sleepy); // the middle one dozes
                _plops[i] = view;
            }
        }

        public void ShowToast(string message)
        {
            _toast.text = message;
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

        private void Update()
        {
            if (_root == null || !_root.gameObject.activeSelf) return;
            float now = Time.unscaledTime;
            for (int i = 0; i < _plops.Length; i++)
            {
                var p = _plops[i];
                if (p == null) continue;
                p.Tick(now);
                if (i == 1 && p.CurrentExpression == Expression.Neutral) p.SetExpression(Expression.Sleepy);
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
