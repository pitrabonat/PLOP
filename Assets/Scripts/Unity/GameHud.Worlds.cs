using System;
using System.Collections;
using System.Collections.Generic;
using Plop.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Plop.Presentation
{
    public sealed partial class GameHud
    {
        private static readonly Vector2 WorldPickerSize = new Vector2(900, 1124);
        private static readonly Vector2 WorldAnnouncementSize = new Vector2(548, 114);
        private const float WorldRowHeight = 155f;
        private const float WorldRowSpacing = 166f;
        private const float AnnouncementSeconds = 1.5f;

        private readonly List<WorldTheme> _pickerWorlds = new List<WorldTheme>();
        private readonly List<WorldRow> _worldRows = new List<WorldRow>();
        private RectTransform _worldPicker, _worldPanel, _worldContent, _worldAnnouncement;
        private ScrollRect _worldScroll;
        private Button _resumeButton;
        private Text _resumeLabel, _worldAnnouncementName;
        private Image _worldAnnouncementArt, _worldAnnouncementBody;
        private CanvasGroup _worldAnnouncementGroup;
        private Coroutine _worldAnnouncementRoutine;
        private int _resumeLevel;

        private sealed class WorldRow
        {
            public RectTransform Root;
            public Button Action;
            public Text Title, Range, Status;
            public Image Preview, Affordance;
            public RectTransform Progress;
        }

        /// <summary>Opens the campaign world list. Level callbacks always use zero-based catalog indices.</summary>
        public void ShowWorlds(IReadOnlyList<WorldTheme> worlds, IReadOnlyList<LevelDefinition> levels,
            Func<int, bool> isUnlocked, Func<int, bool> isCompleted, int currentIndex,
            Func<WorldTheme, Sprite> previewForWorld = null, int resumeIndex = -1)
        {
            if (levels == null || levels.Count == 0) return;
            _pickerWorlds.Clear();
            if (worlds != null)
                foreach (var world in worlds)
                    if (world != null && world.FirstLevel >= 1 && world.FirstLevel <= levels.Count &&
                        world.LastLevel >= world.FirstLevel)
                        _pickerWorlds.Add(world);

            if (_pickerWorlds.Count == 0)
            {
                ShowLevels(levels, isUnlocked, isCompleted, currentIndex);
                return;
            }

            BindPicker(levels, isUnlocked, isCompleted, currentIndex);
            _resumeLevel = Mathf.Clamp(resumeIndex < 0 ? _currentIndex : resumeIndex, 0, levels.Count - 1);
            _resumeLabel.text = "Resume Level " + (_resumeLevel + 1);
            _resumeButton.interactable = _isUnlocked(_resumeLevel);
            RenderWorldRows(previewForWorld);
            _levelPicker.gameObject.SetActive(false);
            SetModal(_worldPicker, true);
            _worldContent.anchoredPosition = Vector2.zero;
        }

        private void BuildWorldPicker()
        {
            _worldPicker = BuildModal("World picker", WorldPickerSize, out _worldPanel);
            var topCenter = new Vector2(0.5f, 1f);
            var bottomCenter = new Vector2(0.5f, 0f);
            var title = MakeText(_worldPanel, "World picker title", HudTheme.Heading, FontStyle.Bold,
                HudTheme.Ink, topCenter, new Vector2(-198, -74), new Vector2(390, 88));
            title.alignment = TextAnchor.MiddleLeft;
            title.text = "Worlds";
            MakeButton(_worldPanel, "Done", new Vector2(310, -74), new Vector2(154, HudTheme.ModalButtonHeight),
                PlopPalette.Milk, CloseLevelPicker, topCenter, fontSize: HudTheme.Control);
            _resumeButton = MakeButton(_worldPanel, "Resume", new Vector2(0, 72), new Vector2(496, 93),
                HudTheme.CompletionSurface, ResumeCampaign, bottomCenter, fontSize: HudTheme.Control);
            _resumeLabel = _resumeButton.GetComponentInChildren<Text>();

            var viewport = NewRect("World viewport", _worldPanel);
            viewport.anchorMin = new Vector2(0.5f, 0f);
            viewport.anchorMax = new Vector2(0.5f, 1f);
            viewport.offsetMin = new Vector2(-407, 134);
            viewport.offsetMax = new Vector2(407, -137);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = Color.clear;

            _worldContent = NewRect("World cards", viewport);
            _worldContent.anchorMin = new Vector2(0f, 1f);
            _worldContent.anchorMax = Vector2.one;
            _worldContent.pivot = topCenter;
            _worldContent.anchoredPosition = Vector2.zero;
            _worldScroll = viewport.gameObject.AddComponent<ScrollRect>();
            _worldScroll.viewport = viewport;
            _worldScroll.content = _worldContent;
            _worldScroll.horizontal = false;
            _worldScroll.movementType = ScrollRect.MovementType.Clamped;
            _worldScroll.scrollSensitivity = 54f;
            _worldPicker.gameObject.SetActive(false);
        }

        private void ResumeCampaign()
        {
            if (_worldPicker.gameObject.activeSelf && _isUnlocked(_resumeLevel))
                LevelChosen?.Invoke(_resumeLevel);
        }

        private void OpenWorld(int slot)
        {
            if (!_worldPicker.gameObject.activeSelf || (uint)slot >= (uint)_pickerWorlds.Count) return;
            var world = _pickerWorlds[slot];
            int first = world.FirstLevel - 1;
            int end = Mathf.Min(world.LastLevel, _pickerLevels.Count);
            if (WorldUnlocked(first, end))
                OpenLevelRange(first, end, world.DisplayName, true);
        }

        private bool WorldUnlocked(int first, int end)
        {
            for (int level = first; level < end; level++)
                if (_isUnlocked(level)) return true;
            return false;
        }

        private void RenderWorldRows(Func<WorldTheme, Sprite> previewForWorld)
        {
            int count = _pickerWorlds.Count;
            while (_worldRows.Count < count)
                _worldRows.Add(BuildWorldRow(_worldRows.Count));

            for (int i = 0; i < _worldRows.Count; i++)
            {
                var row = _worldRows[i];
                bool present = i < count;
                row.Root.gameObject.SetActive(present);
                if (!present) continue;

                var world = _pickerWorlds[i];
                int first = world.FirstLevel - 1;
                int end = Mathf.Min(world.LastLevel, _pickerLevels.Count);
                int complete = 0;
                for (int level = first; level < end; level++)
                    if (_isCompleted(level)) complete++;

                bool unlocked = WorldUnlocked(first, end);
                bool current = _currentIndex >= first && _currentIndex < end;
                row.Action.name = world.DisplayName + " Button";
                row.Action.interactable = unlocked;
                row.Action.image.color = Color.Lerp(PlopPalette.ButtonFill, world.AccentColor,
                    current ? 0.40f : unlocked ? 0.16f : 0.075f);
                row.Title.text = world.DisplayName;
                row.Range.text = "Levels " + (first + 1) + "–" + end + " · " + complete + "/" + (end - first);
                row.Status.text = unlocked ? (current ? "Current" : "") : "Locked";
                row.Affordance.sprite = HudIcons.Get(unlocked ? HudIcons.Kind.Next : HudIcons.Kind.Lock);
                row.Preview.sprite = previewForWorld?.Invoke(world) ?? world.Preview;
                row.Preview.color = row.Preview.sprite == null ? world.AccentColor : Color.white.WithAlpha(unlocked ? 1f : 0.82f);
                row.Progress.anchorMax = new Vector2((float)complete / (end - first), 1f);
                row.Progress.GetComponent<Image>().color = Color.Lerp(world.AccentColor, HudTheme.Ink, 0.32f);
            }

            float height = count * WorldRowSpacing + 11f;
            _worldContent.sizeDelta = new Vector2(0, height);
            _worldScroll.vertical = height > _worldScroll.viewport.rect.height;
        }

        private WorldRow BuildWorldRow(int index)
        {
            var topCenter = new Vector2(0.5f, 1f);
            var center = Vector2.one * 0.5f;
            var root = NewRect("World row", _worldContent);
            root.anchorMin = root.anchorMax = topCenter;
            root.anchoredPosition = new Vector2(0, -83f - index * WorldRowSpacing);
            root.sizeDelta = new Vector2(756f, WorldRowHeight);
            var action = MakeButton(root, "World", Vector2.zero, root.sizeDelta, PlopPalette.ButtonFill,
                () => OpenWorld(index), iconOnly: true);

            var art = NewRect("Chapter art", action.transform);
            art.anchorMin = art.anchorMax = center;
            art.anchoredPosition = new Vector2(-246, 0);
            art.sizeDelta = new Vector2(206, 108);
            var preview = art.gameObject.AddComponent<Image>();
            preview.raycastTarget = false;
            preview.preserveAspect = true;

            var title = MakeText(action.transform, "World name", 40, FontStyle.Bold, HudTheme.Ink, center,
                new Vector2(79, 34), new Vector2(384, 57));
            title.alignment = TextAnchor.MiddleLeft;
            var range = MakeText(action.transform, "World levels and progress", 30, FontStyle.Normal, HudTheme.SecondaryText,
                center, new Vector2(84, -13), new Vector2(394, 42));
            range.alignment = TextAnchor.MiddleLeft;
            var status = MakeText(action.transform, "World status", 26, FontStyle.Bold, HudTheme.SecondaryText, center,
                new Vector2(286, -40), new Vector2(130, 39));
            var affordance = AddGlyph((RectTransform)action.transform, HudIcons.Kind.Next, new Vector2(286, 16), 56f, HudTheme.ActionInk);

            var track = NewRect("World progress track", action.transform);
            track.anchorMin = track.anchorMax = center;
            track.anchoredPosition = new Vector2(33, -50);
            track.sizeDelta = new Vector2(276, 11);
            AddRoundedImage(track, UiSprites.VolumeTrack, Color.Lerp(PlopPalette.Milk, HudTheme.Ink, 0.15f));
            var progress = NewRect("World progress", track);
            AddRoundedImage(progress, UiSprites.VolumeTrack, PlopPalette.Berry);

            return new WorldRow
            {
                Root = root, Action = action, Title = title, Range = range, Status = status,
                Preview = preview, Affordance = affordance, Progress = progress
            };
        }

        /// <summary>Shows a nonmodal unlock notice. Gameplay and level navigation remain available.</summary>
        public void ShowWorldUnlocked(string name, Sprite preview = null, Color? accent = null)
        {
            if (string.IsNullOrWhiteSpace(name) || !isActiveAndEnabled) return;
            CancelWorldAnnouncement();
            _worldAnnouncementName.text = name;
            _worldAnnouncementArt.sprite = preview;
            _worldAnnouncementArt.gameObject.SetActive(preview != null);
            _worldAnnouncementBody.color = Color.Lerp(PlopPalette.ButtonFill, accent ?? PlopPalette.Honey, 0.34f);
            _worldAnnouncement.gameObject.SetActive(true);
            _worldAnnouncementRoutine = StartCoroutine(ShowWorldAnnouncement());
        }

        public void CancelWorldAnnouncement()
        {
            if (_worldAnnouncementRoutine != null)
            {
                StopCoroutine(_worldAnnouncementRoutine);
                _worldAnnouncementRoutine = null;
            }
            if (_worldAnnouncement != null) _worldAnnouncement.gameObject.SetActive(false);
        }

        private void BuildWorldAnnouncement()
        {
            var center = Vector2.one * 0.5f;
            var top = new Vector2(0.5f, 1f);
            _worldAnnouncement = NewRect("World unlock announcement", _safe);
            _worldAnnouncement.anchorMin = _worldAnnouncement.anchorMax = top;
            _worldAnnouncement.anchoredPosition = new Vector2(0, -75);
            _worldAnnouncement.sizeDelta = WorldAnnouncementSize;
            _worldAnnouncementGroup = _worldAnnouncement.gameObject.AddComponent<CanvasGroup>();
            _worldAnnouncementGroup.blocksRaycasts = false;
            _worldAnnouncementGroup.interactable = false;
            AddShadow(_worldAnnouncement, UiSprites.CardShadow, HudTheme.ShadowOffset, HudTheme.CardShadow);
            _worldAnnouncementBody = AddRoundedImage(NewRect("Body", _worldAnnouncement), UiSprites.Card, PlopPalette.ButtonFill);
            var art = NewRect("World unlock art", _worldAnnouncement);
            art.anchorMin = art.anchorMax = center;
            art.anchoredPosition = new Vector2(-170, 0);
            art.sizeDelta = new Vector2(146, 77);
            _worldAnnouncementArt = art.gameObject.AddComponent<Image>();
            _worldAnnouncementArt.preserveAspect = true;
            _worldAnnouncementArt.raycastTarget = false;
            MakeText(_worldAnnouncement, "World unlocked label", 24, FontStyle.Bold, HudTheme.SecondaryText, center,
                new Vector2(67, 23), new Vector2(360, 36)).text = "World unlocked";
            _worldAnnouncementName = MakeText(_worldAnnouncement, "Unlocked world", 39, FontStyle.Bold, HudTheme.Ink,
                center, new Vector2(67, -15), new Vector2(390, 57));
            _worldAnnouncement.gameObject.SetActive(false);
        }

        private IEnumerator ShowWorldAnnouncement()
        {
            float timeStart = Time.unscaledTime;
            for (float elapsed = 0; elapsed < AnnouncementSeconds; elapsed = Time.unscaledTime - timeStart)
            {
                _worldAnnouncementGroup.alpha = _reducedMotion ? 1f :
                    Mathf.Min(Mathf.Clamp01(elapsed / 0.14f), Mathf.Clamp01((AnnouncementSeconds - elapsed) / 0.25f));
                yield return null;
            }
            _worldAnnouncementRoutine = null;
            _worldAnnouncement.gameObject.SetActive(false);
        }

        private void OnDisable() => CancelWorldAnnouncement();
    }
}
