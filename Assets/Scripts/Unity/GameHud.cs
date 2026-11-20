using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Plop.Presentation
{
    /// <summary>
    /// Minimal placeholder HUD built in code (uGUI): level title, move counter, Undo / Restart / Next,
    /// completion banner. Text only changes on gameplay events — no per-frame UI updates.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        public event Action UndoClicked, RestartClicked, NextClicked;

        private Text _title, _moves, _banner;
        private Button _next;

        private void Awake()
        {
            EnsureEventSystem();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("HUD Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // All HUD elements live inside the device safe area (notch / Dynamic Island / home indicator).
            var safe = new GameObject("SafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
            safe.SetParent(canvasGo.transform, false);
            ApplySafeArea(safe);

            _title = MakeText(safe, font, "Title", 64, new Vector2(0.5f, 1f), new Vector2(0, -70));
            _moves = MakeText(safe, font, "Moves", 48, new Vector2(0.5f, 1f), new Vector2(0, -150));
            _banner = MakeText(safe, font, "Banner", 72, new Vector2(0.5f, 0.5f), new Vector2(0, 520));
            _banner.color = new Color(1f, 0.92f, 0.55f);
            _banner.gameObject.SetActive(false);

            MakeButton(safe, font, "Undo", new Vector2(-330, 110), () => UndoClicked?.Invoke());
            MakeButton(safe, font, "Restart", new Vector2(0, 110), () => RestartClicked?.Invoke());
            _next = MakeButton(safe, font, "Next", new Vector2(330, 110), () => NextClicked?.Invoke());
        }

        /// <summary>Fits a full-screen RectTransform to Screen.safeArea (portrait-only game: applied once).</summary>
        private static void ApplySafeArea(RectTransform rt)
        {
            Rect area = Screen.safeArea;
            float w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
            if (area.width <= 0 || area.height <= 0) area = new Rect(0, 0, w, h);
            rt.anchorMin = new Vector2(area.xMin / w, area.yMin / h);
            rt.anchorMax = new Vector2(area.xMax / w, area.yMax / h);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public void SetLevel(string title) => _title.text = title;
        public void SetMoves(int moves) => _moves.text = "Moves: " + moves;

        public void SetCompleted(bool completed, int moves)
        {
            _banner.gameObject.SetActive(completed);
            if (completed) _banner.text = "All Plops asleep!\n" + moves + " moves";
            _next.GetComponent<Image>().color = completed ? new Color(0.45f, 0.8f, 0.5f) : new Color(1f, 1f, 1f, 0.85f);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        private static Text MakeText(Transform parent, Font font, string name, int size, Vector2 anchor, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(1000, 220);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.raycastTarget = false;
            return t;
        }

        private static Button MakeButton(Transform parent, Font font, string label, Vector2 pos, Action onClick)
        {
            var go = new GameObject(label + " Button", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(280, 130);
            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.85f);
            var button = go.AddComponent<Button>();
            button.onClick.AddListener(() => onClick());
            var text = MakeText(go.transform, font, "Label", 48, new Vector2(0.5f, 0.5f), Vector2.zero);
            ((RectTransform)text.transform).sizeDelta = rt.sizeDelta;
            text.color = new Color(0.15f, 0.15f, 0.2f);
            text.text = label;
            return button;
        }
    }
}
