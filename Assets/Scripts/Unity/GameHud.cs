using System;
using System.Collections;
using System.Collections.Generic;
using Plop.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Plop.Presentation
{
    /// <summary>Rounded, safe-area HUD for gameplay, hints and choosing unlocked levels.</summary>
    public sealed class GameHud : MonoBehaviour
    {
        public event Action UndoClicked, RestartClicked, NextClicked, HintClicked, LevelsClicked, ToggleFrameRateClicked;
        public event Action<int> LevelChosen;
        public event Action PickerInteracted;
        public event Action LayoutChanged;

        private const int PageSize = 10;
        private readonly Button[] _levelButtons = new Button[PageSize];
        private readonly Text[] _levelLabels = new Text[PageSize];
        private readonly RectTransform[] _levelSlots = new RectTransform[PageSize];

        private Text _title, _moves, _instruction, _hintStatus, _primaryLabel, _frameRateLabel, _pageLabel;
        private Button _undo, _primary, _previousPage, _nextPage;
        private RectTransform _safe, _card, _levelPicker;
        private CanvasGroup _cardGroup;
        private Coroutine _cardAnim;
        private Font _font;
        private IReadOnlyList<LevelDefinition> _pickerLevels;
        private Func<int, bool> _isUnlocked, _isCompleted;
        private int _page, _currentIndex;
        private bool _completed, _canAdvance;

        public bool IsChoosingLevel => _levelPicker != null && _levelPicker.gameObject.activeSelf;

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

            var topCenter = new Vector2(0.5f, 1f);
            var header = NewRect("Header controls", _safe);
            header.anchorMin = header.anchorMax = topCenter;
            header.anchoredPosition = new Vector2(0, -56);
            header.sizeDelta = new Vector2(820, 80);
            MakePill(header, "Levels", new Vector2(-302, 0), new Vector2(216, 80), PlopPalette.ButtonFill, () => LevelsClicked?.Invoke());
            var rate = MakePill(header, "60 FPS", new Vector2(302, 0), new Vector2(216, 80), PlopPalette.ButtonFill,
                () => ToggleFrameRateClicked?.Invoke());
            _frameRateLabel = rate.GetComponentInChildren<Text>();
            _title = MakeText(_safe, "Title", 58, FontStyle.Bold, PlopPalette.Ink, topCenter, new Vector2(0, -135), new Vector2(900, 80));
            _moves = MakeText(_safe, "Moves", 38, FontStyle.Normal, PlopPalette.MutedText, topCenter, new Vector2(0, -192), new Vector2(900, 52));
            _instruction = MakeText(_safe, "Instruction", 31, FontStyle.Normal, PlopPalette.Ink, topCenter, new Vector2(0, -253), new Vector2(820, 72));
            _instruction.horizontalOverflow = HorizontalWrapMode.Wrap;

            var bottomCenter = new Vector2(0.5f, 0f);
            var buttonsRow = NewRect("Buttons", _safe);
            buttonsRow.anchorMin = buttonsRow.anchorMax = bottomCenter;
            buttonsRow.sizeDelta = new Vector2(864, 122);
            buttonsRow.anchoredPosition = new Vector2(0, 92);
            var buttonSize = new Vector2(252, 122);
            _undo = MakePill(buttonsRow, "Undo", new Vector2(-273, 0), buttonSize, PlopPalette.ButtonFill, () => UndoClicked?.Invoke());
            MakePill(buttonsRow, "Restart", Vector2.zero, buttonSize, PlopPalette.ButtonFill, () => RestartClicked?.Invoke());
            _primary = MakePill(buttonsRow, "Hint", new Vector2(273, 0), buttonSize, PlopPalette.Berry, PrimaryClicked);
            _primaryLabel = _primary.GetComponentInChildren<Text>();
            _hintStatus = MakeText(_safe, "Hint status", 38, FontStyle.Bold, PlopPalette.Ink, bottomCenter,
                new Vector2(0, 228), new Vector2(850, 90));

            _card = NewRect("Completion card", _safe);
            _card.anchorMin = _card.anchorMax = bottomCenter;
            _card.sizeDelta = new Vector2(790, 126);
            _card.anchoredPosition = new Vector2(0, 233);
            _cardGroup = _card.gameObject.AddComponent<CanvasGroup>();
            _cardGroup.blocksRaycasts = false;
            AddShadow(_card, UiSprites.CardShadow, new Vector2(0, -8), 0.16f);
            AddCardBackground(_card);
            MakeText(_card, "Completion message", 48, FontStyle.Bold, PlopPalette.Ink, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(720, 100)).text = "All Plops asleep!";
            _card.gameObject.SetActive(false);

            BuildLevelPicker();
        }

        public void SetLevel(string title, string instruction)
        {
            _title.text = title;
            _instruction.text = instruction ?? "";
            _instruction.gameObject.SetActive(!string.IsNullOrWhiteSpace(instruction));
        }

        public void SetMoves(int moves) => _moves.text = moves == 1 ? "1 move" : moves + " moves";
        public void SetCanUndo(bool canUndo) => _undo.interactable = canUndo;
        public void SetFrameRate(PlopGameController.FrameRateMode mode) => _frameRateLabel.text = (int)mode + " FPS";

        public void SetHint(string message, bool searching)
        {
            _hintStatus.text = message ?? "";
            _primary.interactable = !searching;
        }

        public void SetCompleted(bool completed, bool canAdvance)
        {
            _completed = completed;
            _canAdvance = canAdvance;
            _primaryLabel.text = completed ? (canAdvance ? "Next" : "Levels") : "Hint";
            if (_cardAnim != null)
            {
                StopCoroutine(_cardAnim);
                _cardAnim = null;
            }
            _card.gameObject.SetActive(completed);
            if (completed) _cardAnim = StartCoroutine(PopIn(_card, _cardGroup, 0.45f));
        }

        private void PrimaryClicked()
        {
            if (!_completed) HintClicked?.Invoke();
            else if (_canAdvance) NextClicked?.Invoke();
            else LevelsClicked?.Invoke();
        }

        public void ShowLevels(IReadOnlyList<LevelDefinition> levels, Func<int, bool> isUnlocked,
            Func<int, bool> isCompleted, int currentIndex)
        {
            _pickerLevels = levels;
            _isUnlocked = isUnlocked;
            _isCompleted = isCompleted;
            _currentIndex = currentIndex;
            _page = currentIndex / PageSize;
            RenderLevelPage();
            _levelPicker.gameObject.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(null);
        }

        public void CloseLevelPicker()
        {
            if (!IsChoosingLevel) return;
            _levelPicker.gameObject.SetActive(false);
            EventSystem.current?.SetSelectedGameObject(null);
            PickerInteracted?.Invoke();
        }

        private void BuildLevelPicker()
        {
            _levelPicker = NewRect("Level picker", _safe);
            // Capture pointer events over the background while the modal is visible.
            var blocker = _levelPicker.gameObject.AddComponent<Image>();
            blocker.color = Color.clear;

            var panel = NewRect("Level picker card", _levelPicker);
            panel.anchorMin = new Vector2(0.04f, 0.5f);
            panel.anchorMax = new Vector2(0.96f, 0.5f);
            panel.offsetMin = new Vector2(0, -490);
            panel.offsetMax = new Vector2(0, 490);
            AddShadow(panel, UiSprites.CardShadow, new Vector2(0, -8), 0.22f);
            AddCardBackground(panel);

            MakeText(panel, "Picker title", 52, FontStyle.Bold, PlopPalette.Ink, new Vector2(0.42f, 1f),
                new Vector2(0, -74), new Vector2(460, 80)).text = "Choose a level";
            MakePill(panel, "Close", new Vector2(0, -74), new Vector2(172, 79), PlopPalette.Milk,
                CloseLevelPicker, new Vector2(0.85f, 1f));

            for (int slot = 0; slot < PageSize; slot++)
            {
                int offset = slot;
                float x = slot % 2 == 0 ? 0.265f : 0.735f;
                float y = -204 - slot / 2 * 138;
                _levelSlots[slot] = NewRect("Level slot " + slot, panel);
                _levelButtons[slot] = MakePill(_levelSlots[slot], "Level", new Vector2(0, y), new Vector2(340, 102),
                    PlopPalette.Milk, () => LevelChosen?.Invoke(_page * PageSize + offset), new Vector2(x, 1f));
                _levelLabels[slot] = _levelButtons[slot].GetComponentInChildren<Text>();
            }

            _previousPage = MakePill(panel, "Back", new Vector2(0, 79), new Vector2(170, 80), PlopPalette.Milk,
                () => ChangeLevelPage(-1), new Vector2(0.20f, 0));
            _nextPage = MakePill(panel, "More", new Vector2(0, 79), new Vector2(170, 80), PlopPalette.Milk,
                () => ChangeLevelPage(1), new Vector2(0.80f, 0));
            _pageLabel = MakeText(panel, "Level range", 32, FontStyle.Normal, PlopPalette.MutedText,
                new Vector2(0.5f, 0), new Vector2(0, 79), new Vector2(350, 65));
            _levelPicker.gameObject.SetActive(false);
        }

        private void ChangeLevelPage(int offset)
        {
            _page += offset;
            RenderLevelPage();
            PickerInteracted?.Invoke();
        }

        private void RenderLevelPage()
        {
            int start = _page * PageSize;
            int end = Mathf.Min(start + PageSize, _pickerLevels.Count);
            for (int slot = 0; slot < PageSize; slot++)
            {
                int index = start + slot;
                var button = _levelButtons[slot];
                bool present = index < end;
                _levelSlots[slot].gameObject.SetActive(present);
                if (!present) continue;

                bool unlocked = _isUnlocked(index);
                bool done = _isCompleted(index);
                button.interactable = unlocked;
                _levelLabels[slot].text = "Level " + (index + 1) + (done ? " · Done" : unlocked ? "" : " · Locked");
                button.image.color = index == _currentIndex ? PlopPalette.Berry : done ? PlopPalette.Mint : PlopPalette.Milk;
            }
            _previousPage.interactable = _page > 0;
            _nextPage.interactable = end < _pickerLevels.Count;
            _pageLabel.text = (start + 1) + "–" + end + " of " + _pickerLevels.Count;
        }

        private static void AddCardBackground(RectTransform parent)
        {
            var body = NewRect("Body", parent);
            var image = body.gameObject.AddComponent<Image>();
            image.sprite = UiSprites.Card;
            image.type = Image.Type.Sliced;
            image.color = PlopPalette.ButtonFill;
            image.raycastTarget = false;
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

        private Button MakePill(RectTransform parent, string label, Vector2 pos, Vector2 size, Color fill, Action onClick, Vector2? anchor = null)
        {
            var rt = NewRect(label + " Button", parent);
            rt.anchorMin = rt.anchorMax = anchor ?? new Vector2(0.5f, 0.5f);
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
