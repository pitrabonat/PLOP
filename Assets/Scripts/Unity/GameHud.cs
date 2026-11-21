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

        private Text _title, _moves, _instruction, _hintStatus, _completionActionLabel, _pageLabel;
        private Text _deadlockTitle, _deadlockDescription;
        private Text _musicPercent, _soundPercent;
        private Slider _music, _sound;
        private Image _completionActionIcon, _deadlockMascot;
        private Button _undo, _hint, _previousPage, _nextPage;
        private Button _deadlockUndo, _deadlockRestart;
        private Button _rate30, _rate60;
        private RectTransform _safe, _header, _status, _footer, _dock, _completion;
        private RectTransform _levelBadge, _moveBadge;
        private RectTransform _levelPicker, _settings, _deadlock;
        private RectTransform _pickerPanel, _settingsPanel, _deadlockPanel;
        private CanvasGroup _gameplayGroup;
        private CanvasGroup _cardGroup;
        private Coroutine _cardAnim;
        private Font _font, _displayFont;
        private static readonly Vector2 PickerSize = new Vector2(880, 980);
        private static readonly Vector2 SettingsSize = new Vector2(840, 652);
        private static readonly Vector2 DeadlockSize = new Vector2(742, 660);
        private static readonly Vector2 DeadlockTextOnlySize = new Vector2(742, 524);
        private IReadOnlyList<LevelDefinition> _pickerLevels;
        private Func<int, bool> _isUnlocked, _isCompleted;
        private int _page, _currentIndex;
        private bool _completed, _canAdvance, _hasBoardLayout;
        private BoardLayout.Result _boardLayout;

        public bool IsChoosingLevel => _levelPicker != null && _levelPicker.gameObject.activeSelf;
        public bool IsSettingsOpen => _settings != null && _settings.gameObject.activeSelf;
        public bool IsDeadlockOpen => _deadlock != null && _deadlock.gameObject.activeSelf;
        public bool BlocksGameplayInput => IsChoosingLevel || IsSettingsOpen || IsDeadlockOpen;

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
            var center = Vector2.one * 0.5f;
            var bottomCenter = new Vector2(0.5f, 0f);
            _header = NewRect("Header", gameplay);
            _header.anchorMin = _header.anchorMax = new Vector2(0.5f, 1f);
            _header.anchoredPosition = new Vector2(0, -80);
            _header.sizeDelta = new Vector2(HudTheme.HeaderWidth, HudTheme.HeaderButtonHeight);
            var headerButtonSize = new Vector2(124, HudTheme.HeaderButtonHeight);
            MakeButton(_header, "Levels", new Vector2(-378, 0), headerButtonSize, PlopPalette.ButtonFill,
                () => LevelsClicked?.Invoke(), icon: HudIcons.Kind.Levels, touchHeight: HudTheme.PlayTouchHeight, iconOnly: true);
            var name = MakeText(_header, "Game name", 84, FontStyle.Bold, HudTheme.Ink, center,
                Vector2.zero, new Vector2(438, 128));
            name.text = "PLOP!";
            MakeButton(_header, "Settings", new Vector2(378, 0), headerButtonSize, PlopPalette.ButtonFill,
                () => SettingsClicked?.Invoke(), icon: HudIcons.Kind.Settings, touchHeight: HudTheme.PlayTouchHeight, iconOnly: true);

            _status = NewRect("Board status", gameplay);
            _status.anchorMin = _status.anchorMax = center;
            _status.sizeDelta = new Vector2(HudTheme.StatusWidth, 120);
            _levelBadge = MakeBadge(_status, "Level badge", new Vector2(466, 103), PlopPalette.ButtonFill);
            _title = MakeText(_levelBadge, "Title", HudTheme.Display, FontStyle.Bold, HudTheme.Ink, center,
                Vector2.zero, new Vector2(440, 108));
            _title.verticalOverflow = VerticalWrapMode.Overflow;
            _moveBadge = MakeBadge(_status, "Move badge", new Vector2(300, 74), HudTheme.MoveSurface);
            AddGlyph(_moveBadge, HudIcons.Kind.Moves, new Vector2(-100, 0), 47f, HudTheme.ActionInk);
            _moves = MakeText(_moveBadge, "Moves", 37, FontStyle.Bold, HudTheme.Ink, center,
                new Vector2(20, 0), new Vector2(235, 62));
            _instruction = MakeText(_status, "Instruction", HudTheme.Caption, FontStyle.Normal, HudTheme.SecondaryText,
                center, new Vector2(0, 30), new Vector2(HudTheme.StatusWidth, 70));
            _instruction.horizontalOverflow = HorizontalWrapMode.Wrap;
            _instruction.verticalOverflow = VerticalWrapMode.Overflow;

            _dock = NewRect("Action dock", gameplay);
            _dock.anchorMin = _dock.anchorMax = bottomCenter;
            _dock.anchoredPosition = new Vector2(0, HudTheme.FooterRestY);
            _dock.sizeDelta = new Vector2(HudTheme.DeckWidth, HudTheme.FooterButtonHeight);
            _footer = NewRect("Board actions", _dock);
            var buttonSize = new Vector2(177, HudTheme.FooterButtonHeight);
            _undo = MakeButton(_footer, "Undo", new Vector2(-218, 0), buttonSize, PlopPalette.ButtonFill,
                () => UndoClicked?.Invoke(), icon: HudIcons.Kind.Undo, touchHeight: HudTheme.PlayTouchHeight, stackIcon: true);
            MakeButton(_footer, "Restart", Vector2.zero, buttonSize, PlopPalette.ButtonFill,
                () => RestartClicked?.Invoke(), icon: HudIcons.Kind.Restart, touchHeight: HudTheme.PlayTouchHeight, stackIcon: true);
            _hint = MakeButton(_footer, "Hint", new Vector2(218, 0), buttonSize, HudTheme.HintSurface,
                () => HintClicked?.Invoke(), icon: HudIcons.Kind.Hint, touchHeight: HudTheme.PlayTouchHeight, stackIcon: true);
            _hintStatus = MakeText(_footer, "Hint status", 34, FontStyle.Bold, HudTheme.Ink, Vector2.one * 0.5f,
                new Vector2(0, 111), new Vector2(HudTheme.DeckWidth, 62));
            _hintStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
            _hintStatus.verticalOverflow = VerticalWrapMode.Overflow;

            BuildCompletion();
            BuildLevelPicker();
            BuildSettings();
            BuildDeadlock();
            FitResponsiveElements();
            PositionPlayControls();
        }

        public void SetBoardLayout(BoardLayout.Result layout)
        {
            _boardLayout = layout;
            _hasBoardLayout = true;
            FitPlayRows();
            PositionPlayControls();
        }

        private void PositionPlayControls()
        {
            const float edgeGap = 10f;
            bool showInstruction = _instruction.gameObject.activeSelf && _instruction.text.Length > 0;
            float moveY = showInstruction ? 104f : 49f;
            float levelY = moveY + 98f;
            _moveBadge.anchoredPosition = new Vector2(0, moveY);
            _levelBadge.anchoredPosition = new Vector2(0, levelY);
            if (!_hasBoardLayout) return;

            float scale = _boardLayout.CanvasScale;
            float safeBottom = _boardLayout.SafePixels.yMin;
            float safeHeight = _boardLayout.SafePixels.height / scale;
            float boardTop = (_boardLayout.BoardRectPixels.yMax - safeBottom) / scale;
            float boardBottom = (_boardLayout.BoardRectPixels.yMin - safeBottom) / scale;
            float headerScale = _header.localScale.y;
            float deckScale = _dock.localScale.y;
            float headerY = boardTop + edgeGap + levelY * _status.localScale.y + 132f * headerScale;
            var bottomCenter = new Vector2(0.5f, 0f);
            _header.anchorMin = _header.anchorMax = bottomCenter;
            _status.anchorMin = _status.anchorMax = bottomCenter;
            _header.anchoredPosition = new Vector2(0, Mathf.Min(safeHeight - 80f * headerScale, headerY));
            _status.anchoredPosition = new Vector2(0, boardTop + edgeGap);
            _dock.anchoredPosition = new Vector2(0,
                Mathf.Clamp(boardBottom - 152f * deckScale, HudTheme.FooterRestY * deckScale, 235f));
        }

        public void SetLevel(string title, string instruction)
        {
            _title.text = title;
            _instruction.text = instruction ?? "";
            bool showInstruction = !string.IsNullOrWhiteSpace(instruction);
            _instruction.gameObject.SetActive(showInstruction);
            PositionPlayControls();
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
            _hint.interactable = !searching;
        }

        public bool ShowDeadlock(bool canUndo, Sprite mascot = null)
        {
            if (_completed) return false;
            if (IsDeadlockOpen) return true;
            if (BlocksGameplayInput) return false;

            _deadlockUndo.interactable = canUndo;
            _deadlockUndo.image.color = canUndo ? HudTheme.RecoverySurface : PlopPalette.Milk;
            _deadlockRestart.image.color = canUndo ? PlopPalette.Milk : HudTheme.RecoverySurface;

            bool illustrated = mascot != null;
            _deadlockMascot.sprite = mascot;
            _deadlockMascot.gameObject.SetActive(illustrated);
            _deadlockTitle.rectTransform.anchoredPosition = new Vector2(0, illustrated ? -236f : -100f);
            _deadlockDescription.rectTransform.anchoredPosition = new Vector2(0, illustrated ? -328f : -192f);
            _deadlockPanel.sizeDelta = illustrated ? DeadlockSize : DeadlockTextOnlySize;
            FitModal(_deadlockPanel, _deadlockPanel.sizeDelta);
            SetModal(_deadlock, true);
            return true;
        }

        public void CloseDeadlock()
        {
            if (IsDeadlockOpen) SetModal(_deadlock, false);
        }

        public void SetCompleted(bool completed, bool canAdvance)
        {
            _completed = completed;
            _canAdvance = canAdvance;
            if (completed) CloseDeadlock();
            _completionActionLabel.text = canAdvance ? "Next Level" : "Levels";
            _completionActionIcon.sprite = HudIcons.Get(canAdvance ? HudIcons.Kind.Next : HudIcons.Kind.Levels);

            if (_cardAnim != null)
            {
                StopCoroutine(_cardAnim);
                _cardAnim = null;
            }
            _footer.gameObject.SetActive(!completed);
            _completion.gameObject.SetActive(completed);
            if (completed) _cardAnim = StartCoroutine(PopIn(_completion, _cardGroup));
        }

        private void BuildCompletion()
        {
            _completion = NewRect("Completion card", _dock);
            _completion.anchorMin = _completion.anchorMax = Vector2.one * 0.5f;
            _completion.sizeDelta = new Vector2(HudTheme.DeckWidth, 206);
            _completion.anchoredPosition = new Vector2(0, 12);
            _cardGroup = _completion.gameObject.AddComponent<CanvasGroup>();
            AddShadow(_completion, UiSprites.CardShadow, HudTheme.ShadowOffset, HudTheme.CardShadow);
            AddCardBackground(_completion, HudTheme.CompletionSurface);
            MakeText(_completion, "Completion message", HudTheme.Heading, FontStyle.Bold, HudTheme.Ink,
                Vector2.one * 0.5f, new Vector2(0, 48), new Vector2(590, 69)).text = "Sweet Dreams!";
            MakeButton(_completion, "Replay", new Vector2(-161, -43), new Vector2(252, 76), PlopPalette.ButtonFill,
                () => RestartClicked?.Invoke(), icon: HudIcons.Kind.Restart, fontSize: HudTheme.Control, touchHeight: 114f);
            var advance = MakeButton(_completion, "Next Level", new Vector2(130, -43), new Vector2(297, 76),
                Color.Lerp(PlopPalette.ButtonFill, PlopPalette.Mint, 0.62f),
                () =>
                {
                    if (_canAdvance) NextClicked?.Invoke();
                    else LevelsClicked?.Invoke();
                }, icon: HudIcons.Kind.Next, fontSize: HudTheme.Action, touchHeight: 114f);
            _completionActionLabel = advance.GetComponentInChildren<Text>();
            _completionActionIcon = advance.transform.Find("Icon").GetComponent<Image>();
            _completion.gameObject.SetActive(false);
        }

        private void BuildDeadlock()
        {
            _deadlock = BuildModal("Deadlock", DeadlockSize, out _deadlockPanel);
            var top = new Vector2(0.5f, 1f);
            var bottom = new Vector2(0.5f, 0f);
            var portrait = NewRect("Worried Plop", _deadlockPanel);
            portrait.anchorMin = portrait.anchorMax = top;
            portrait.anchoredPosition = new Vector2(0, -120);
            portrait.sizeDelta = new Vector2(150, 136);
            _deadlockMascot = portrait.gameObject.AddComponent<Image>();
            _deadlockMascot.preserveAspect = true;
            _deadlockMascot.raycastTarget = false;
            _deadlockTitle = MakeText(_deadlockPanel, "Deadlock title", 54, FontStyle.Bold, HudTheme.Ink,
                top, new Vector2(0, -236), new Vector2(672, 80));
            _deadlockTitle.text = "Oops, we're stuck!";
            _deadlockDescription = MakeText(_deadlockPanel, "Deadlock explanation", 36, FontStyle.Normal, HudTheme.SecondaryText,
                top, new Vector2(0, -328), new Vector2(590, 122));
            _deadlockDescription.horizontalOverflow = HorizontalWrapMode.Wrap;
            _deadlockDescription.verticalOverflow = VerticalWrapMode.Overflow;
            _deadlockDescription.text = "There's no way to get everyone home from here.";

            _deadlockUndo = MakeButton(_deadlockPanel, "Undo Move", new Vector2(-162, 175), new Vector2(292, 100),
                HudTheme.RecoverySurface, () =>
                {
                    CloseDeadlock();
                    UndoClicked?.Invoke();
                }, bottom, fontSize: HudTheme.Control, touchHeight: 120f);
            _deadlockRestart = MakeButton(_deadlockPanel, "Restart Level", new Vector2(153, 175), new Vector2(307, 100),
                PlopPalette.Milk, () =>
                {
                    CloseDeadlock();
                    RestartClicked?.Invoke();
                }, bottom, fontSize: HudTheme.Control, touchHeight: 120f);
            MakeButton(_deadlockPanel, "Keep playing", new Vector2(0, 61), new Vector2(240, 74),
                PlopPalette.ButtonFill, CloseDeadlock, bottom, fontSize: HudTheme.Caption, touchHeight: 102f);
            _deadlock.gameObject.SetActive(false);
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

            MakeText(panel, "Picker title", HudTheme.Heading, FontStyle.Bold, HudTheme.Ink, new Vector2(0.40f, 1f),
                new Vector2(0, -74), new Vector2(460, 80)).text = "Choose a level";
            MakeButton(panel, "Close", new Vector2(0, -74), new Vector2(173, HudTheme.ModalButtonHeight), PlopPalette.Milk,
                CloseLevelPicker, new Vector2(0.85f, 1f), fontSize: HudTheme.Control);

            for (int slot = 0; slot < PageSize; slot++)
            {
                int offset = slot;
                float x = slot % 2 == 0 ? 0.265f : 0.735f;
                float y = -204 - slot / 2 * 138;
                _levelSlots[slot] = NewRect("Level slot " + slot, panel);
                _levelButtons[slot] = MakeButton(_levelSlots[slot], "Level", new Vector2(0, y), new Vector2(340, 102),
                    PlopPalette.Milk, () => LevelChosen?.Invoke(_page * PageSize + offset), new Vector2(x, 1f), fontSize: HudTheme.Control);
                _levelLabels[slot] = _levelButtons[slot].GetComponentInChildren<Text>();
            }

            _previousPage = MakeButton(panel, "Back", new Vector2(0, 79), new Vector2(170, 80), PlopPalette.Milk,
                () => ChangeLevelPage(-1), new Vector2(0.20f, 0));
            _nextPage = MakeButton(panel, "More", new Vector2(0, 79), new Vector2(170, 80), PlopPalette.Milk,
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
            var title = MakeText(panel, "Settings title", HudTheme.Heading, FontStyle.Bold, HudTheme.Ink, new Vector2(0.12f, 1f),
                new Vector2(177, -70), new Vector2(352, 80));
            title.alignment = TextAnchor.MiddleLeft;
            title.text = "Settings";
            MakeButton(panel, "Done", new Vector2(0, -70), new Vector2(168, HudTheme.ModalButtonHeight),
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
            MakeText(panel, "Frame rate title", HudTheme.Control, FontStyle.Bold, HudTheme.Ink, center,
                new Vector2(0, -456), new Vector2(670, 53)).text = "Frame rate";
            _rate30 = MakeButton(panel, "30 FPS", new Vector2(-171, -546), new Vector2(308, 94), PlopPalette.Milk,
                () => FrameRateChanged?.Invoke(PlopGameController.FrameRateMode.Fps30), center);
            _rate60 = MakeButton(panel, "60 FPS", new Vector2(171, -546), new Vector2(308, 94), PlopPalette.Milk,
                () => FrameRateChanged?.Invoke(PlopGameController.FrameRateMode.Fps60), center);
            _settings.gameObject.SetActive(false);
        }

        private Slider MakeVolume(Transform panel, string label, float y, out Text percent, Action<float> changed)
        {
            var anchor = new Vector2(0.5f, 1f);
            var name = MakeText(panel, label + " label", HudTheme.Control, FontStyle.Bold, HudTheme.Ink, anchor,
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
            blocker.color = HudTheme.Ink.WithAlpha(0.18f);
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

        private void FitResponsiveElements()
        {
            if (_deadlockPanel == null) return;
            FitModal(_pickerPanel, PickerSize);
            FitModal(_settingsPanel, SettingsSize);
            FitModal(_deadlockPanel, _deadlockPanel.sizeDelta);
            FitPlayRows();
        }

        private void FitPlayRows()
        {
            FitRow(_header, HudTheme.HeaderWidth);
            FitRow(_status, HudTheme.StatusWidth);
            FitRow(_dock, HudTheme.DeckWidth);
        }

        private void FitRow(RectTransform row, float width)
        {
            float reserve = _hasBoardLayout ? _boardLayout.ReserveScale : 1f;
            float widthFit = (_safe.rect.width - HudTheme.ModalMargin * 2f) / width;
            float scale = Mathf.Clamp(Mathf.Min(widthFit, reserve), 0.25f, 1f);
            row.localScale = new Vector3(scale, scale, 1f);
        }

        private void FitModal(RectTransform panel, Vector2 nominal)
        {
            var space = _safe.rect.size - Vector2.one * (2 * HudTheme.ModalMargin);
            float scale = Mathf.Clamp(Mathf.Min(space.x / nominal.x, space.y / nominal.y), 0.25f, 1f);
            panel.localScale = new Vector3(scale, scale, 1f);
        }

        private static RectTransform MakeBadge(Transform parent, string name, Vector2 size, Color color)
        {
            var badge = NewRect(name, parent);
            badge.anchorMin = badge.anchorMax = Vector2.one * 0.5f;
            badge.sizeDelta = size;
            AddShadow(badge, UiSprites.ControlShadow, HudTheme.ShadowOffset, HudTheme.ButtonShadow);
            AddRoundedImage(NewRect("Body", badge), UiSprites.Badge, color);
            return badge;
        }

        private static Image AddGlyph(RectTransform parent, HudIcons.Kind icon, Vector2 pos, float size, Color color)
        {
            var glyph = NewRect("Icon", parent);
            glyph.anchorMin = glyph.anchorMax = Vector2.one * 0.5f;
            glyph.anchoredPosition = pos;
            glyph.sizeDelta = Vector2.one * size;
            var image = glyph.gameObject.AddComponent<Image>();
            image.sprite = HudIcons.Get(icon);
            image.color = color;
            image.raycastTarget = false;
            return image;
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
            FitResponsiveElements();
            if (_instruction != null) PositionPlayControls();
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

        private Button MakeButton(RectTransform parent, string label, Vector2 pos, Vector2 size, Color fill,
            Action onClick, Vector2? anchor = null, HudIcons.Kind? icon = null, int fontSize = HudTheme.Action,
            float touchHeight = 0f, bool stackIcon = false, bool iconOnly = false)
        {
            var rt = NewRect(label + " Button", parent);
            rt.anchorMin = rt.anchorMax = anchor ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size.x, Mathf.Max(size.y, touchHeight));
            rt.gameObject.AddComponent<Image>().color = Color.clear;

            // The cast shadow stays in place as the face responds to a press.
            var shadowHolder = NewRect(label + " Shadow", parent);
            shadowHolder.anchorMin = shadowHolder.anchorMax = rt.anchorMin;
            shadowHolder.anchoredPosition = pos + HudTheme.ShadowOffset;
            float spread = UiSprites.ShadowSpread(UiSprites.ControlShadow);
            shadowHolder.sizeDelta = size + new Vector2(2 * spread, 2 * spread);
            var shadowImg = shadowHolder.gameObject.AddComponent<Image>();
            shadowImg.sprite = UiSprites.ControlShadow;
            shadowImg.type = Image.Type.Sliced;
            shadowImg.color = PlopPalette.Shadow.WithAlpha(HudTheme.ButtonShadow);
            shadowImg.raycastTarget = false;
            shadowHolder.SetSiblingIndex(rt.GetSiblingIndex());

            var center = Vector2.one * 0.5f;
            var face = NewRect("Face", rt);
            face.anchorMin = face.anchorMax = center;
            face.sizeDelta = size;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = AddRoundedImage(face, UiSprites.Control, fill);
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

            if (icon.HasValue)
            {
                var kind = icon.Value;
                Vector2 glyphPosition = iconOnly ? Vector2.zero :
                    stackIcon ? new Vector2(0, 19) : new Vector2(-size.x * 0.34f, 0);
                float glyphSize = iconOnly ? 66f : stackIcon ? 64f : HudTheme.Icon;
                Color ink = kind == HudIcons.Kind.Hint
                    ? Color.Lerp(HudTheme.Ink, PlopPalette.Honey, 0.44f)
                    : HudTheme.ActionInk;
                AddGlyph(rt, kind, glyphPosition, glyphSize, ink);
            }

            if (!iconOnly)
            {
                Vector2 labelPosition = stackIcon ? new Vector2(0, -35) :
                    new Vector2(icon.HasValue ? 19f : 0f, 0f);
                var labelBox = stackIcon ? new Vector2(size.x, 44f) : size;
                var text = MakeText(rt, "Label", stackIcon ? HudTheme.Caption : fontSize, FontStyle.Bold,
                    HudTheme.Ink, center, labelPosition, labelBox);
                text.text = label;
            }
            return button;
        }
    }
}
