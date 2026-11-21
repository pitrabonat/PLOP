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
        public event Action UndoClicked, RestartClicked, NextClicked, HintClicked, LevelsClicked;
        public event Action SettingsClicked, SettingsClosed, ButtonActivated;
        public event Action<PlopGameController.FrameRateMode> FrameRateChanged;
        public event Action<float> MusicChanged, SoundChanged;
        public event Action<int> LevelChosen;
        public event Action ModalInteracted;
        public event Action LayoutChanged;

        private const int PageSize = 10;
        private readonly Button[] _levelButtons = new Button[PageSize];
        private readonly Text[] _levelLabels = new Text[PageSize];
        private readonly RectTransform[] _levelSlots = new RectTransform[PageSize];

        private Text _title, _moves, _instruction, _hintStatus, _primaryLabel, _pageLabel;
        private Text _musicPercent, _soundPercent;
        private Slider _music, _sound;
        private Image _primaryIcon;
        private Button _undo, _primary, _previousPage, _nextPage;
        private Button _rate30, _rate60;
        private RectTransform _safe, _card, _levelPicker, _settings;
        private RectTransform _pickerPanel, _settingsPanel;
        private CanvasGroup _gameplayGroup;
        private CanvasGroup _cardGroup;
        private Coroutine _cardAnim;
        private Font _font, _displayFont;
        private static readonly Vector2 PickerSize = new Vector2(880, 980);
        private static readonly Vector2 SettingsSize = new Vector2(840, 652);
        private IReadOnlyList<LevelDefinition> _pickerLevels;
        private Func<int, bool> _isUnlocked, _isCompleted;
        private int _page, _currentIndex;
        private bool _completed, _canAdvance;

        public bool IsChoosingLevel => _levelPicker != null && _levelPicker.gameObject.activeSelf;
        public bool IsSettingsOpen => _settings != null && _settings.gameObject.activeSelf;
        public bool BlocksGameplayInput => IsChoosingLevel || IsSettingsOpen;

        private void Awake()
        {
            EnsureEventSystem();
            _font = Resources.Load<Font>("Typography/Nunito-Text") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _displayFont = Resources.Load<Font>("Typography/Nunito-Display") ?? _font;

            var canvasGo = new GameObject("HUD Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = BoardLayout.ReferenceResolution;
            scaler.matchWidthOrHeight = HudTheme.CanvasMatch;
            canvasGo.AddComponent<GraphicRaycaster>();

            _safe = NewRect("SafeArea", canvasGo.transform);
            _safe.gameObject.AddComponent<RectChangeNotifier>().Changed += OnRectChanged;
            ApplySafeArea();

            var gameplay = NewRect("Play controls", _safe);
            _gameplayGroup = gameplay.gameObject.AddComponent<CanvasGroup>();
            var topCenter = new Vector2(0.5f, 1f);
            var header = NewRect("Header controls", gameplay);
            header.anchorMin = header.anchorMax = topCenter;
            header.anchoredPosition = new Vector2(0, -64);
            header.sizeDelta = new Vector2(856, HudTheme.HeaderButtonHeight);
            MakePill(header, "Levels", new Vector2(-308, 0), new Vector2(213, HudTheme.HeaderButtonHeight),
                PlopPalette.ButtonFill, () => LevelsClicked?.Invoke(), icon: HudIcons.Kind.Levels, fontSize: HudTheme.Control);
            MakeText(header, "Game name", 47, FontStyle.Bold, PlopPalette.Ink, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(200, 72)).text = "PLOP!";
            MakePill(header, "Settings", new Vector2(308, 0), new Vector2(224, HudTheme.HeaderButtonHeight),
                PlopPalette.ButtonFill, () => SettingsClicked?.Invoke(), icon: HudIcons.Kind.Settings, fontSize: HudTheme.Control);

            _title = MakeText(gameplay, "Title", HudTheme.Display, FontStyle.Bold, PlopPalette.Ink, topCenter,
                new Vector2(0, -159), new Vector2(846, 120));
            _title.verticalOverflow = VerticalWrapMode.Overflow;
            _moves = MakeText(gameplay, "Moves", 37, FontStyle.Normal, HudTheme.SecondaryText, topCenter,
                new Vector2(0, -223), new Vector2(800, 55));
            _instruction = MakeText(gameplay, "Instruction", HudTheme.Caption, FontStyle.Normal, PlopPalette.Ink, topCenter,
                new Vector2(0, -281), new Vector2(790, 76));
            _instruction.horizontalOverflow = HorizontalWrapMode.Wrap;

            var bottomCenter = new Vector2(0.5f, 0f);
            var buttonsRow = NewRect("Buttons", gameplay);
            buttonsRow.anchorMin = buttonsRow.anchorMax = bottomCenter;
            buttonsRow.sizeDelta = new Vector2(852, HudTheme.FooterButtonHeight);
            buttonsRow.anchoredPosition = new Vector2(0, 91);
            var buttonSize = new Vector2(252, HudTheme.FooterButtonHeight);
            _undo = MakePill(buttonsRow, "Undo", new Vector2(-271, 0), buttonSize, PlopPalette.ButtonFill,
                () => UndoClicked?.Invoke(), icon: HudIcons.Kind.Undo);
            MakePill(buttonsRow, "Restart", Vector2.zero, buttonSize, PlopPalette.ButtonFill,
                () => RestartClicked?.Invoke(), icon: HudIcons.Kind.Restart);
            _primary = MakePill(buttonsRow, "Hint", new Vector2(271, 0), buttonSize, PlopPalette.Berry,
                PrimaryClicked, icon: HudIcons.Kind.Hint);
            _primaryLabel = _primary.GetComponentInChildren<Text>();
            _primaryIcon = _primary.transform.Find("Icon").GetComponent<Image>();
            _hintStatus = MakeText(gameplay, "Hint status", 38, FontStyle.Bold, PlopPalette.Ink, bottomCenter,
                new Vector2(0, 220), new Vector2(823, 82));

            _card = NewRect("Completion card", gameplay);
            _card.anchorMin = _card.anchorMax = bottomCenter;
            _card.sizeDelta = new Vector2(791, 108);
            _card.anchoredPosition = new Vector2(0, 221);
            _cardGroup = _card.gameObject.AddComponent<CanvasGroup>();
            _cardGroup.blocksRaycasts = false;
            AddShadow(_card, UiSprites.CardShadow, HudTheme.ShadowOffset, HudTheme.CardShadow);
            AddCardBackground(_card, Color.Lerp(PlopPalette.ButtonFill, PlopPalette.Mint, 0.33f));
            MakeText(_card, "Completion message", HudTheme.Heading, FontStyle.Bold, PlopPalette.Ink, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(720, 96)).text = "Sweet Dreams!";
            _card.gameObject.SetActive(false);

            BuildLevelPicker();
            BuildSettings();
            FitModals();
        }

        public void SetLevel(string title, string instruction)
        {
            _title.text = title;
            _instruction.text = instruction ?? "";
            _instruction.gameObject.SetActive(!string.IsNullOrWhiteSpace(instruction));
        }

        public void SetMoves(int moves) => _moves.text = moves == 1 ? "1 move" : moves + " moves";
        public void SetCanUndo(bool canUndo) => _undo.interactable = canUndo;
        public void SetFrameRate(PlopGameController.FrameRateMode mode)
        {
            _rate30.image.color = mode == PlopGameController.FrameRateMode.Fps30 ? PlopPalette.Berry : PlopPalette.Milk;
            _rate60.image.color = mode == PlopGameController.FrameRateMode.Fps60 ? PlopPalette.Berry : PlopPalette.Milk;
        }

        public void SetVolumes(float music, float sound)
        {
            _music.SetValueWithoutNotify(music);
            _sound.SetValueWithoutNotify(sound);
            _musicPercent.text = Percent(music);
            _soundPercent.text = Percent(sound);
        }

        public void ShowSettings() => SetModal(_settings, true);

        public void CloseSettings()
        {
            if (!IsSettingsOpen) return;
            SetModal(_settings, false);
            SettingsClosed?.Invoke();
        }

        public void SetHint(string message, bool searching)
        {
            _hintStatus.text = message ?? "";
            _primary.interactable = !searching;
        }

        public void SetCompleted(bool completed, bool canAdvance)
        {
            _completed = completed;
            _canAdvance = canAdvance;
            _primaryLabel.text = completed ? (canAdvance ? "Next Level" : "Levels") : "Hint";
            _primaryLabel.fontSize = completed && canAdvance ? 35 : HudTheme.Action;
            _primaryLabel.rectTransform.anchoredPosition = new Vector2(completed && canAdvance ? 24f : 19f, 0f);
            _primaryIcon.sprite = HudIcons.Get(!completed ? HudIcons.Kind.Hint : canAdvance ? HudIcons.Kind.Next : HudIcons.Kind.Levels);
            if (_cardAnim != null)
            {
                StopCoroutine(_cardAnim);
                _cardAnim = null;
            }
            _card.gameObject.SetActive(completed);
            if (completed) _cardAnim = StartCoroutine(PopIn(_card, _cardGroup));
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
            SetModal(_levelPicker, true);
        }

        public void CloseLevelPicker()
        {
            if (!IsChoosingLevel) return;
            SetModal(_levelPicker, false);
        }

        private void BuildLevelPicker()
        {
            _levelPicker = BuildModal("Level picker", PickerSize, out _pickerPanel);
            var panel = _pickerPanel;

            MakeText(panel, "Picker title", HudTheme.Heading, FontStyle.Bold, PlopPalette.Ink, new Vector2(0.40f, 1f),
                new Vector2(0, -74), new Vector2(460, 80)).text = "Choose a level";
            MakePill(panel, "Close", new Vector2(0, -74), new Vector2(173, HudTheme.ModalButtonHeight), PlopPalette.Milk,
                CloseLevelPicker, new Vector2(0.85f, 1f), fontSize: HudTheme.Control);

            for (int slot = 0; slot < PageSize; slot++)
            {
                int offset = slot;
                float x = slot % 2 == 0 ? 0.265f : 0.735f;
                float y = -204 - slot / 2 * 138;
                _levelSlots[slot] = NewRect("Level slot " + slot, panel);
                _levelButtons[slot] = MakePill(_levelSlots[slot], "Level", new Vector2(0, y), new Vector2(340, 102),
                    PlopPalette.Milk, () => LevelChosen?.Invoke(_page * PageSize + offset), new Vector2(x, 1f), fontSize: HudTheme.Control);
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
            ModalInteracted?.Invoke();
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

        private void BuildSettings()
        {
            _settings = BuildModal("Settings", SettingsSize, out _settingsPanel);
            var panel = _settingsPanel;
            var title = MakeText(panel, "Settings title", HudTheme.Heading, FontStyle.Bold, PlopPalette.Ink, new Vector2(0.12f, 1f),
                new Vector2(177, -70), new Vector2(352, 80));
            title.alignment = TextAnchor.MiddleLeft;
            title.text = "Settings";
            MakePill(panel, "Done", new Vector2(0, -70), new Vector2(168, HudTheme.ModalButtonHeight),
                PlopPalette.Milk, CloseSettings, new Vector2(0.84f, 1f), fontSize: HudTheme.Control);

            _music = MakeVolume(panel, "Music", -180, out _musicPercent, value =>
            {
                _musicPercent.text = Percent(value);
                MusicChanged?.Invoke(value);
                ModalInteracted?.Invoke();
            });
            _sound = MakeVolume(panel, "Sound", -309, out _soundPercent, value =>
            {
                _soundPercent.text = Percent(value);
                SoundChanged?.Invoke(value);
                ModalInteracted?.Invoke();
            });

            var center = new Vector2(0.5f, 1f);
            MakeText(panel, "Frame rate title", HudTheme.Control, FontStyle.Bold, PlopPalette.Ink, center,
                new Vector2(0, -456), new Vector2(670, 53)).text = "Frame rate";
            _rate30 = MakePill(panel, "30 FPS", new Vector2(-171, -546), new Vector2(308, 94), PlopPalette.Milk,
                () => FrameRateChanged?.Invoke(PlopGameController.FrameRateMode.Fps30), center);
            _rate60 = MakePill(panel, "60 FPS", new Vector2(171, -546), new Vector2(308, 94), PlopPalette.Milk,
                () => FrameRateChanged?.Invoke(PlopGameController.FrameRateMode.Fps60), center);
            _settings.gameObject.SetActive(false);
        }

        private Slider MakeVolume(Transform panel, string label, float y, out Text percent, Action<float> changed)
        {
            var anchor = new Vector2(0.5f, 1f);
            var name = MakeText(panel, label + " label", HudTheme.Control, FontStyle.Bold, PlopPalette.Ink, anchor,
                new Vector2(-162, y), new Vector2(338, 54));
            name.alignment = TextAnchor.MiddleLeft;
            name.text = label;
            percent = MakeText(panel, label + " value", HudTheme.Caption, FontStyle.Normal, HudTheme.SecondaryText, anchor,
                new Vector2(268, y), new Vector2(124, 54));
            percent.alignment = TextAnchor.MiddleRight;

            var root = NewRect(label + " volume", panel);
            root.anchorMin = root.anchorMax = anchor;
            root.anchoredPosition = new Vector2(0, y - 57);
            root.sizeDelta = new Vector2(696, 84);
            root.gameObject.AddComponent<Image>().color = Color.clear;
            var slider = root.gameObject.AddComponent<Slider>();

            var rail = NewRect("Track", root);
            rail.anchorMin = rail.anchorMax = new Vector2(0.5f, 0.5f);
            rail.sizeDelta = new Vector2(646, HudTheme.VolumeTrackRadius * 2f);
            AddRoundedImage(rail, UiSprites.VolumeTrack, Color.Lerp(PlopPalette.Milk, PlopPalette.Shadow, 0.19f));
            var fill = NewRect("Fill", rail);
            AddRoundedImage(fill, UiSprites.VolumeTrack, PlopPalette.Mint);

            var bounds = NewRect("Handle bounds", root);
            // Slider drives the handle's vertical anchors to stretch. Its container supplies the height.
            bounds.anchorMin = new Vector2(0f, 0.5f);
            bounds.anchorMax = new Vector2(1f, 0.5f);
            bounds.anchoredPosition = Vector2.zero;
            bounds.sizeDelta = new Vector2(-50f, HudTheme.VolumeHandleSize);
            var thumb = NewRect("Handle", bounds);
            thumb.anchorMin = Vector2.zero;
            thumb.anchorMax = Vector2.up;
            thumb.sizeDelta = new Vector2(HudTheme.VolumeHandleSize, 0f);
            var image = AddRoundedImage(thumb, UiSprites.Pill, PlopPalette.Berry);
            image.type = Image.Type.Simple;

            slider.fillRect = fill;
            slider.handleRect = thumb;
            slider.targetGraphic = image;
            slider.onValueChanged.AddListener(value => changed(value));
            return slider;
        }

        private static string Percent(float value) => Mathf.RoundToInt(value * 100f) + "%";

        private RectTransform BuildModal(string name, Vector2 size, out RectTransform panel)
        {
            var container = NewRect(name, _safe);
            var blocker = container.gameObject.AddComponent<Image>();
            blocker.color = PlopPalette.Ink.WithAlpha(0.18f);
            panel = NewRect(name + " card", container);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = size;
            AddShadow(panel, UiSprites.CardShadow, HudTheme.ShadowOffset, HudTheme.CardShadow);
            AddCardBackground(panel, PlopPalette.ButtonFill);
            return container;
        }

        private void SetModal(RectTransform modal, bool open)
        {
            modal.gameObject.SetActive(open);
            _gameplayGroup.interactable = _gameplayGroup.blocksRaycasts = !BlocksGameplayInput;
            EventSystem.current?.SetSelectedGameObject(null);
            ModalInteracted?.Invoke();
        }

        private void FitModals()
        {
            if (_pickerPanel == null || _settingsPanel == null) return;
            FitModal(_pickerPanel, PickerSize);
            FitModal(_settingsPanel, SettingsSize);
        }

        private void FitModal(RectTransform panel, Vector2 nominal)
        {
            var space = _safe.rect.size - Vector2.one * (2 * HudTheme.ModalMargin);
            float scale = Mathf.Clamp(Mathf.Min(space.x / nominal.x, space.y / nominal.y), 0.25f, 1f);
            panel.localScale = new Vector3(scale, scale, 1f);
        }

        private static void AddCardBackground(RectTransform parent, Color color)
            => AddRoundedImage(NewRect("Body", parent), UiSprites.Card, color);

        private static Image AddRoundedImage(RectTransform rect, Sprite sprite, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Header + footer reserve in canvas reference units (consumed by BoardLayout).</summary>
        public static Vector2 Reserved => new Vector2(BoardLayout.HeaderHeight, BoardLayout.FooterHeight);

        // ------------------------------------------------------------------ layout

        private void OnRectChanged()
        {
            ApplySafeArea();
            FitModals();
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

        private static IEnumerator PopIn(RectTransform rt, CanvasGroup group)
        {
            group.alpha = 0f;
            rt.localScale = new Vector3(0.94f, 0.94f, 1f);
            float t0 = Time.unscaledTime;
            const float duration = HudTheme.CompletionEntrance;
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
            t.font = style == FontStyle.Bold ? _displayFont : _font;
            t.fontSize = size;
            t.fontStyle = _font == _displayFont ? style : FontStyle.Normal;
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

        private Button MakePill(RectTransform parent, string label, Vector2 pos, Vector2 size, Color fill,
            Action onClick, Vector2? anchor = null, HudIcons.Kind? icon = null, int fontSize = HudTheme.Action)
        {
            var rt = NewRect(label + " Button", parent);
            rt.anchorMin = rt.anchorMax = anchor ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            // the shadow is a sibling placed behind the button so it does not squish with the press
            var shadowHolder = NewRect(label + " Shadow", parent);
            shadowHolder.anchorMin = shadowHolder.anchorMax = rt.anchorMin;
            shadowHolder.anchoredPosition = pos + HudTheme.ShadowOffset;
            float spread = UiSprites.ShadowSpread(UiSprites.PillShadow);
            shadowHolder.sizeDelta = size + new Vector2(2 * spread, 2 * spread);
            var shadowImg = shadowHolder.gameObject.AddComponent<Image>();
            shadowImg.sprite = UiSprites.PillShadow;
            shadowImg.type = Image.Type.Sliced;
            shadowImg.color = PlopPalette.Shadow.WithAlpha(HudTheme.ButtonShadow);
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
            colors.pressedColor = PlopPalette.ButtonPressed;
            colors.disabledColor = PlopPalette.ButtonDisabled;
            colors.fadeDuration = HudTheme.TintDuration;
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                ButtonActivated?.Invoke();
                onClick();
            });
            rt.gameObject.AddComponent<PressScale>();

            var center = new Vector2(0.5f, 0.5f);
            if (icon.HasValue)
            {
                var glyph = NewRect("Icon", rt);
                glyph.anchorMin = glyph.anchorMax = center;
                glyph.anchoredPosition = new Vector2(-size.x * 0.34f, 0);
                glyph.sizeDelta = Vector2.one * HudTheme.Icon;
                var iconImage = glyph.gameObject.AddComponent<Image>();
                iconImage.sprite = HudIcons.Get(icon.Value);
                iconImage.color = PlopPalette.Ink;
                iconImage.raycastTarget = false;
            }

            var text = MakeText(rt, "Label", fontSize, FontStyle.Bold, PlopPalette.Ink, center,
                new Vector2(icon.HasValue ? 19f : 0f, 0), size);
            text.text = label;
            return button;
        }
    }
}
