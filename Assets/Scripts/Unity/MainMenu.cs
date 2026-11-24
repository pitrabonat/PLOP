using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Plop.Presentation
{
    /// <summary>
    /// Portrait main menu in Soft Toy Minimalism, built in code (uGUI) above the gameplay HUD.
    /// Hierarchy: logo → sleepy jelly Plops → large PLAY → Daily Gift · Daily Puzzle → Levels → Settings.
    /// Minimal text, rounded controls, comfortable touch targets, everything inside the safe area.
    /// The Plops reuse the gameplay atlas sprites; their idle bob runs only while the menu is visible.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        public event Action PlayClicked, DailyGiftClicked, DailyPuzzleClicked, LevelsClicked, SettingsClicked;

        private const float FadeSeconds = 0.22f;
        private RectTransform _root, _safe, _mascots;
        private CanvasGroup _group;
        private Text _dust, _playSubtitle, _giftLabel, _dailyLabel, _toast;
        private Image _giftDot, _dailyCheck;
        private Button _giftButton;
        private Font _font, _display;
        private readonly RectTransform[] _plopBodies = new RectTransform[3];
        private readonly Image[][] _plopEyes = { new Image[2], new Image[2], new Image[2] };
        private Sprite _eyeOpen, _eyeClosed;
        private float[] _nextBlink = new float[3];
        private Coroutine _fade, _toastAnim;
        private bool _reducedMotion;

        /// <summary>True while the menu covers the board and takes input (false once a fade-out has begun).</summary>
        public bool IsOpen => _root != null && _root.gameObject.activeSelf && _group.blocksRaycasts;
        public bool ReducedMotion { set => _reducedMotion = value; }

        private void Awake()
        {
            _font = Resources.Load<Font>("Typography/Nunito-Text") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _display = Resources.Load<Font>("Typography/Nunito-Display") ?? _font;

            var canvasGo = new GameObject("Main Menu Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            _root = (RectTransform)canvasGo.transform;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50; // above the gameplay HUD
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = BoardLayout.ReferenceResolution;
            scaler.matchWidthOrHeight = HudTheme.CanvasMatch;
            canvasGo.AddComponent<GraphicRaycaster>();
            _group = canvasGo.AddComponent<CanvasGroup>();

            var background = Rect("Background", _root);
            var bg = background.gameObject.AddComponent<Image>();
            bg.color = PlopPalette.PlayBackground;

            _safe = Rect("SafeArea", _root);
            _safe.gameObject.AddComponent<RectChangeNotifier>().Changed += ApplySafeArea;
            ApplySafeArea();

            // ---- top bar: Settings (left), Dream Dust balance (right) ----
            var settings = Pill(_safe, "Menu Settings", new Vector2(0f, 1f), new Vector2(110, -100), new Vector2(124, 116),
                PlopPalette.ButtonFill, null, HudIcons.Kind.Settings, () => SettingsClicked?.Invoke());
            settings.GetComponentInChildren<Text>()?.gameObject.SetActive(false);

            var dustPill = Card(_safe, "Dream Dust", new Vector2(1f, 1f), new Vector2(-150, -100), new Vector2(236, 96), PlopPalette.ButtonFill);
            Glyph(dustPill, HudIcons.Kind.Star, new Vector2(-66, 0), 54, PlopPalette.Honey);
            _dust = Label(dustPill, "Dust amount", HudTheme.Action, FontStyle.Bold, HudTheme.Ink, new Vector2(24, 0), new Vector2(140, 70));

            // ---- logo ----
            var logo = Label(_safe, "Logo", 196, FontStyle.Bold, HudTheme.Ink, new Vector2(0, 560), new Vector2(900, 230), _display);
            logo.text = "PLOP!";
            var tagline = Label(_safe, "Tagline", HudTheme.Caption + 4, FontStyle.Bold, HudTheme.SecondaryText, new Vector2(0, 436), new Vector2(700, 60));
            tagline.text = "Sleepy Puzzle";

            // ---- jelly Plops on a pillow ----
            _mascots = Rect("Mascots", _safe);
            _mascots.anchorMin = _mascots.anchorMax = new Vector2(0.5f, 0.5f);
            _mascots.sizeDelta = new Vector2(760, 330);
            _mascots.anchoredPosition = new Vector2(0, 170);
            var pillow = Card(_mascots, "Pillow", new Vector2(0.5f, 0.5f), new Vector2(0, -110), new Vector2(640, 110),
                Color.Lerp(PlopPalette.TrayRim, PlopPalette.Berry, 0.18f));
            pillow.SetAsFirstSibling();
            var atlas = ShapeAtlas.ForDensity(256);
            _eyeOpen = atlas.Get("eye");
            _eyeClosed = atlas.Get("eyeClosed");
            int[] colors = { 0, 1, 2 };
            float[] xs = { -220f, 0f, 220f };
            for (int i = 0; i < 3; i++) BuildPlop(i, atlas, colors[i], new Vector2(xs[i], -112f), i == 1 ? 300f : 250f);

            // ---- PLAY ----
            var play = Pill(_safe, "Play", new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(600, 176),
                Color.Lerp(PlopPalette.Berry, PlopPalette.ButtonFill, 0.12f), "Play", HudIcons.Kind.Play, () => PlayClicked?.Invoke(), 64);
            _playSubtitle = Label(play, "Play level", HudTheme.Caption, FontStyle.Bold, HudTheme.ActionInk, new Vector2(40, -52), new Vector2(360, 44));
            var playLabel = play.Find("Label");
            if (playLabel != null) ((RectTransform)playLabel).anchoredPosition = new Vector2(40, 14);

            // ---- Daily Gift · Daily Puzzle ----
            _giftButton = Tile(_safe, "Daily Gift", new Vector2(-165, -420), HudIcons.Kind.Gift, PlopPalette.Honey, () => DailyGiftClicked?.Invoke(), out _giftLabel);
            _giftDot = Glyph((RectTransform)_giftButton.transform, HudIcons.Kind.Star, new Vector2(108, 64), 40, PlopPalette.Berry);
            _giftDot.name = "Gift available";
            var daily = Tile(_safe, "Daily Puzzle", new Vector2(165, -420), HudIcons.Kind.Calendar, PlopPalette.Mint, () => DailyPuzzleClicked?.Invoke(), out _dailyLabel);
            _dailyCheck = Glyph((RectTransform)daily.transform, HudIcons.Kind.Check, new Vector2(108, 64), 44, Color.Lerp(PlopPalette.Mint, PlopPalette.Ink, 0.45f));
            _dailyCheck.name = "Daily done";

            // ---- Levels ----
            Pill(_safe, "Levels", new Vector2(0.5f, 0.5f), new Vector2(0, -640), new Vector2(380, 116), PlopPalette.ButtonFill,
                "Levels", HudIcons.Kind.Levels, () => LevelsClicked?.Invoke());

            _toast = Label(_safe, "Toast", HudTheme.Action, FontStyle.Bold, HudTheme.Ink, new Vector2(0, -300), new Vector2(800, 60));
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

        // ------------------------------------------------------------------ animation

        private void Update()
        {
            if (_root == null || !_root.gameObject.activeSelf) return;
            float t = Time.unscaledTime;
            for (int i = 0; i < 3; i++)
            {
                float breath = _reducedMotion ? 0f : Mathf.Sin(t * 2.1f + i * 1.7f);
                _plopBodies[i].localScale = new Vector3(1f - 0.025f * breath, 1f + 0.035f * breath, 1f);
                bool blink = t >= _nextBlink[i] && t < _nextBlink[i] + 0.14f;
                if (t >= _nextBlink[i] + 0.14f) _nextBlink[i] = t + 2.2f + ((i * 7919 + (int)(t * 13)) % 30) * 0.1f;
                var eye = i == 1 || blink ? _eyeClosed : _eyeOpen; // the middle Plop dozes; the others blink
                _plopEyes[i][0].sprite = eye;
                _plopEyes[i][1].sprite = eye;
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

        // ------------------------------------------------------------------ builders

        private void BuildPlop(int index, ShapeAtlas atlas, int colorId, Vector2 ground, float unitsPerCell)
        {
            var root = Rect("Plop " + index, _mascots);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.zero;
            root.anchoredPosition = ground;
            var shadow = AtlasImage(root, atlas, atlas.Get("plopShadow"), new Vector2(0f, 0.005f), unitsPerCell);
            shadow.name = "Shadow";
            var body = AtlasImage(root, atlas, atlas.Get(ShapeAtlas.PlopKey(colorId)), Vector2.zero, unitsPerCell);
            body.name = "Body";
            _plopBodies[index] = body.rectTransform;
            AtlasImage(body.rectTransform, atlas, atlas.Get("cheek"), new Vector2(-0.219f, 0.320f), unitsPerCell, relativeToPivot: true);
            AtlasImage(body.rectTransform, atlas, atlas.Get("cheek"), new Vector2(0.204f, 0.319f), unitsPerCell, relativeToPivot: true);
            _plopEyes[index][0] = AtlasImage(body.rectTransform, atlas, _eyeOpen, new Vector2(-0.138f, 0.407f), unitsPerCell, relativeToPivot: true);
            _plopEyes[index][1] = AtlasImage(body.rectTransform, atlas, _eyeOpen, new Vector2(0.122f, 0.403f), unitsPerCell, relativeToPivot: true);
            AtlasImage(body.rectTransform, atlas, atlas.Get(index == 1 ? "mouthSleep" : "mouth"), new Vector2(-0.005f, 0.300f), unitsPerCell, relativeToPivot: true);
            _nextBlink[index] = 1.2f + index * 0.9f;
        }

        /// <summary>Places an atlas sprite as a UI image so its pivot lands at <paramref name="world"/> (cell units).</summary>
        private static Image AtlasImage(RectTransform parent, ShapeAtlas atlas, Sprite sprite, Vector2 world, float unitsPerCell,
            bool relativeToPivot = false)
        {
            var rt = Rect(sprite.name, parent);
            float ppu = atlas.PixelsPerUnit;
            var size = sprite.rect.size / ppu * unitsPerCell;
            rt.anchorMin = rt.anchorMax = relativeToPivot ? (Vector2)parent.pivot : new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
            rt.sizeDelta = size;
            rt.anchoredPosition = world * unitsPerCell;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        private void ApplySafeArea()
        {
            Rect area = Screen.safeArea;
            float w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
            if (area.width <= 0 || area.height <= 0) area = new Rect(0, 0, w, h);
            _safe.anchorMin = new Vector2(area.xMin / w, area.yMin / h);
            _safe.anchorMax = new Vector2(area.xMax / w, area.yMax / h);
            _safe.offsetMin = _safe.offsetMax = Vector2.zero;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        private Text Label(Transform parent, string name, int size, FontStyle style, Color color, Vector2 pos, Vector2 box, Font font = null)
        {
            var rt = Rect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font ?? _font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static Image Glyph(RectTransform parent, HudIcons.Kind kind, Vector2 pos, float size, Color color)
        {
            var rt = Rect("Icon", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = HudIcons.Get(kind);
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static RectTransform Card(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color fill)
        {
            var shadow = Rect(name + " Shadow", parent);
            shadow.anchorMin = shadow.anchorMax = anchor;
            shadow.anchoredPosition = pos + HudTheme.ShadowOffset;
            shadow.sizeDelta = size + Vector2.one * (2 * HudTheme.CardShadowSpread);
            var s = shadow.gameObject.AddComponent<Image>();
            s.sprite = UiSprites.CardShadow;
            s.type = Image.Type.Sliced;
            s.color = PlopPalette.Shadow.WithAlpha(HudTheme.CardShadow);
            s.raycastTarget = false;

            var rt = Rect(name, parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UiSprites.Card;
            img.type = Image.Type.Sliced;
            img.color = fill;
            return rt;
        }

        private RectTransform Pill(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color fill,
            string label, HudIcons.Kind icon, Action onClick, int fontSize = HudTheme.Action)
        {
            var shadow = Rect(name + " Shadow", parent);
            shadow.anchorMin = shadow.anchorMax = anchor;
            shadow.anchoredPosition = pos + HudTheme.ShadowOffset;
            shadow.sizeDelta = size + Vector2.one * (2 * HudTheme.PillShadowSpread);
            var s = shadow.gameObject.AddComponent<Image>();
            s.sprite = UiSprites.PillShadow;
            s.type = Image.Type.Sliced;
            s.color = PlopPalette.Shadow.WithAlpha(HudTheme.ButtonShadow);
            s.raycastTarget = false;

            var rt = Rect(name + " Button", parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UiSprites.Pill;
            img.type = Image.Type.Sliced;
            img.color = fill;
            var button = rt.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.pressedColor = new Color(0.94f, 0.92f, 0.90f);
            colors.fadeDuration = HudTheme.TintDuration;
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            rt.gameObject.AddComponent<PressScale>();
            bool hasLabel = !string.IsNullOrEmpty(label);
            Glyph(rt, icon, hasLabel ? new Vector2(-size.x * 0.5f + size.y * 0.55f, 0) : Vector2.zero, Mathf.Min(64, size.y * 0.55f), HudTheme.ActionInk);
            if (hasLabel)
            {
                var text = Label(rt, "Label", fontSize, FontStyle.Bold, HudTheme.Ink, new Vector2(size.y * 0.25f, 0), new Vector2(size.x - size.y, size.y));
                text.text = label;
            }
            return rt;
        }

        private Button Tile(RectTransform parent, string name, Vector2 pos, HudIcons.Kind icon, Color accent, Action onClick, out Text label)
        {
            var rt = Card(parent, name, new Vector2(0.5f, 0.5f), pos, new Vector2(300, 210), Color.Lerp(PlopPalette.ButtonFill, accent, 0.32f));
            var button = rt.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.pressedColor = new Color(0.94f, 0.92f, 0.90f);
            colors.fadeDuration = HudTheme.TintDuration;
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            rt.gameObject.AddComponent<PressScale>();
            rt.name = name + " Button";
            Glyph(rt, icon, new Vector2(0, 28), 92, Color.Lerp(accent, PlopPalette.Ink, 0.45f));
            label = Label(rt, "Label", HudTheme.Control, FontStyle.Bold, HudTheme.Ink, new Vector2(0, -62), new Vector2(280, 50));
            return button;
        }
    }
}
