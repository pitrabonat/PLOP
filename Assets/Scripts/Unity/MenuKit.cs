using System;
using UnityEngine;
using UnityEngine.UI;

namespace Plop.Presentation
{
    /// <summary>Shared builders for the code-built menus (main menu, Character Studio): rounded cards, pills, labels.</summary>
    internal sealed class MenuKit
    {
        public readonly Font Font, Display;

        public MenuKit()
        {
            Font = Resources.Load<Font>("Typography/Nunito-Text") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Display = Resources.Load<Font>("Typography/Nunito-Display") ?? Font;
        }

        /// <summary>A full-screen overlay canvas at the given sorting order, plus its CanvasGroup.</summary>
        public static RectTransform Canvas(Transform owner, string name, int sortingOrder, out CanvasGroup group)
        {
            var canvasGo = new GameObject(name, typeof(RectTransform));
            canvasGo.transform.SetParent(owner, false);
            var canvas = canvasGo.AddComponent<UnityEngine.Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = BoardLayout.ReferenceResolution;
            scaler.matchWidthOrHeight = HudTheme.CanvasMatch;
            canvasGo.AddComponent<GraphicRaycaster>();
            group = canvasGo.AddComponent<CanvasGroup>();
            return (RectTransform)canvasGo.transform;
        }

        public static void FitSafeArea(RectTransform safe)
        {
            Rect area = Screen.safeArea;
            float w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
            if (area.width <= 0 || area.height <= 0) area = new Rect(0, 0, w, h);
            safe.anchorMin = new Vector2(area.xMin / w, area.yMin / h);
            safe.anchorMax = new Vector2(area.xMax / w, area.yMax / h);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public Text Label(Transform parent, string name, int size, FontStyle style, Color color, Vector2 pos, Vector2 box, Font font = null)
        {
            var rt = Rect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font ?? Font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Image Glyph(RectTransform parent, HudIcons.Kind kind, Vector2 pos, float size, Color color)
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

        public static RectTransform Card(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color fill)
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

        public RectTransform Pill(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color fill,
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

        public Button Tile(RectTransform parent, string name, Vector2 pos, HudIcons.Kind icon, Color accent, Action onClick, out Text label)
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
