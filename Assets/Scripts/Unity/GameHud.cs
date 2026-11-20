using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Plop.Presentation
{
    /// <summary>
    /// Gameplay HUD in "Soft Toy Minimalism", built in code (uGUI): level title + move counter at the top,
    /// rounded pill Undo / Restart at the bottom, and a small completion card with a "Next" action that
    /// replaces the button row. Everything lives inside Screen.safeArea; sizes are canvas reference units
    /// (1080×1920, match 0.5), the same values <see cref="BoardLayout"/> reserves for the board.
    /// Text changes only on gameplay events.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        public event Action UndoClicked, RestartClicked, NextClicked;
        /// <summary>Raised when the screen size or safe area changes (resize, split screen, rotation).</summary>
        public event Action LayoutChanged;

        private Text _title, _moves, _cardTitle, _cardSubtitle;
        private Button _undo, _restart, _next;
        private RectTransform _safe, _buttonsRow, _card;
        private CanvasGroup _cardGroup;
        private Coroutine _cardAnim;
        private Font _font;

        private void Awake()
        {
            EnsureEventSystem();
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("HUD Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = BoardLayout.ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            _safe = NewRect("SafeArea", canvasGo.transform);
            _safe.gameObject.AddComponent<RectChangeNotifier>().Changed += OnRectChanged;
            ApplySafeArea();

            // ---- header ----
            _title = MakeText(_safe, "Title", 58, FontStyle.Bold, PlopPalette.Ink, new Vector2(0.5f, 1f), new Vector2(0, -92), new Vector2(900, 90));
            _moves = MakeText(_safe, "Moves", 38, FontStyle.Normal, PlopPalette.MutedText, new Vector2(0.5f, 1f), new Vector2(0, -160), new Vector2(900, 60));

            // ---- footer: pill buttons ----
            _buttonsRow = NewRect("Buttons", _safe);
            _buttonsRow.anchorMin = _buttonsRow.anchorMax = new Vector2(0.5f, 0f);
            _buttonsRow.sizeDelta = new Vector2(760, BoardLayout.FooterHeight);
            _buttonsRow.anchoredPosition = new Vector2(0, BoardLayout.FooterHeight * 0.5f);
            _undo = MakePill(_buttonsRow, "Undo", new Vector2(-180, -10), new Vector2(320, 128), PlopPalette.ButtonFill, () => UndoClicked?.Invoke());
            _restart = MakePill(_buttonsRow, "Restart", new Vector2(180, -10), new Vector2(320, 128), PlopPalette.ButtonFill, () => RestartClicked?.Invoke());

            // ---- completion card (replaces the button row) ----
            _card = NewRect("CompletionCard", _safe);
            _card.anchorMin = _card.anchorMax = new Vector2(0.5f, 0f);
            _card.sizeDelta = new Vector2(940, 220);
            _card.anchoredPosition = new Vector2(0, BoardLayout.FooterHeight * 0.5f - 6);
            _cardGroup = _card.gameObject.AddComponent<CanvasGroup>();
            AddShadow(_card, UiSprites.CardShadow, new Vector2(0, -8), 0.16f); // first child = drawn behind
            var body = NewRect("Body", _card);
            var cardBg = body.gameObject.AddComponent<Image>();
            cardBg.sprite = UiSprites.Card;
            cardBg.type = Image.Type.Sliced;
            cardBg.color = PlopPalette.ButtonFill;
            _cardTitle = MakeText(body, "CardTitle", 48, FontStyle.Bold, PlopPalette.Ink, new Vector2(0f, 0.5f), new Vector2(300, 34), new Vector2(520, 70));
            _cardTitle.alignment = TextAnchor.MiddleLeft;
            _cardTitle.text = "Sweet dreams!";
            _cardSubtitle = MakeText(body, "CardSubtitle", 34, FontStyle.Normal, PlopPalette.MutedText, new Vector2(0f, 0.5f), new Vector2(300, -32), new Vector2(520, 56));
            _cardSubtitle.alignment = TextAnchor.MiddleLeft;
            _next = MakePill(body, "Next", new Vector2(250, 0), new Vector2(300, 128), PlopPalette.Berry, () => NextClicked?.Invoke());
            _card.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ API

        public void SetLevel(string title) => _title.text = title;
        public void SetMoves(int moves) => _moves.text = moves == 1 ? "1 move" : moves + " moves";
        public void SetCanUndo(bool canUndo) => _undo.interactable = canUndo;

        public void SetCompleted(bool completed, int moves)
        {
            if (completed)
            {
                _cardSubtitle.text = "All Plops asleep · " + (moves == 1 ? "1 move" : moves + " moves");
                _buttonsRow.gameObject.SetActive(false);
                _card.gameObject.SetActive(true);
                if (_cardAnim != null) StopCoroutine(_cardAnim);
                _cardAnim = StartCoroutine(PopIn(_card, _cardGroup, 0.45f));
            }
            else
            {
                if (_cardAnim != null) { StopCoroutine(_cardAnim); _cardAnim = null; }
                _card.gameObject.SetActive(false);
                _buttonsRow.gameObject.SetActive(true);
            }
        }

        /// <summary>Header + footer reserve in canvas reference units (consumed by BoardLayout).</summary>
        public static Vector2 Reserved => new Vector2(BoardLayout.HeaderHeight, BoardLayout.FooterHeight);

        // ------------------------------------------------------------------ layout

        private void OnRectChanged()
        {
            ApplySafeArea();
            LayoutChanged?.Invoke();
        }

        private void ApplySafeArea()
        {
            Rect area = Screen.safeArea;
            float w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
            if (area.width <= 0 || area.height <= 0) area = new Rect(0, 0, w, h);
            var min = new Vector2(area.xMin / w, area.yMin / h);
            var max = new Vector2(area.xMax / w, area.yMax / h);
            if (_safe.anchorMin == min && _safe.anchorMax == max) return;
            _safe.anchorMin = min;
            _safe.anchorMax = max;
            _safe.offsetMin = _safe.offsetMax = Vector2.zero;
        }

        /// <summary>Card entrance after the Plops' completion hop: short delay, gentle scale + fade.</summary>
        private static IEnumerator PopIn(RectTransform rt, CanvasGroup group, float delay)
        {
            group.alpha = 0f;
            rt.localScale = new Vector3(0.94f, 0.94f, 1f);
            float t0 = Time.unscaledTime + delay;
            while (Time.unscaledTime < t0) yield return null;
            const float duration = 0.22f;
            for (float u = 0f; u < 1f; u = (Time.unscaledTime - t0) / duration)
            {
                float e = 1f - (1f - u) * (1f - u);
                group.alpha = e;
                float s = Mathf.Lerp(0.94f, 1f, e) + 0.02f * Mathf.Sin(Mathf.PI * u);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            group.alpha = 1f;
            rt.localScale = Vector3.one;
        }

        // ------------------------------------------------------------------ builders

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        private Text MakeText(Transform parent, string name, int size, FontStyle style, Color color, Vector2 anchor, Vector2 pos, Vector2 box)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            var t = rt.gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        private static void AddShadow(RectTransform target, Sprite shadowSprite, Vector2 offset, float alpha)
        {
            var rt = NewRect("Shadow", target);
            float spread = UiSprites.ShadowSpread(shadowSprite);
            rt.offsetMin = new Vector2(-spread, -spread) + offset;
            rt.offsetMax = new Vector2(spread, spread) + offset;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = shadowSprite;
            img.type = Image.Type.Sliced;
            img.color = PlopPalette.Shadow.WithAlpha(alpha);
            img.raycastTarget = false;
            rt.SetAsFirstSibling();
        }

        private Button MakePill(RectTransform parent, string label, Vector2 pos, Vector2 size, Color fill, Action onClick)
        {
            var rt = NewRect(label + " Button", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            // the shadow is a sibling placed behind the button so it does not squish with the press
            var shadowHolder = NewRect(label + " Shadow", parent);
            shadowHolder.anchorMin = shadowHolder.anchorMax = rt.anchorMin;
            shadowHolder.anchoredPosition = pos + new Vector2(0, -7);
            float spread = UiSprites.ShadowSpread(UiSprites.PillShadow);
            shadowHolder.sizeDelta = size + new Vector2(2 * spread, 2 * spread);
            var shadowImg = shadowHolder.gameObject.AddComponent<Image>();
            shadowImg.sprite = UiSprites.PillShadow;
            shadowImg.type = Image.Type.Sliced;
            shadowImg.color = PlopPalette.Shadow.WithAlpha(0.18f);
            shadowImg.raycastTarget = false;
            shadowHolder.SetSiblingIndex(rt.GetSiblingIndex());

            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UiSprites.Pill;
            img.type = Image.Type.Sliced;
            img.color = fill;
            var button = rt.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.94f, 0.92f, 0.90f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            rt.gameObject.AddComponent<PressScale>();

            var text = MakeText(rt, "Label", 42, FontStyle.Bold, PlopPalette.Ink, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            text.text = label;
            return button;
        }
    }
}
