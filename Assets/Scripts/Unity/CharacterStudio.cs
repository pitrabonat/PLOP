using System;
using System.Collections.Generic;
using Plop.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace Plop.Presentation
{
    /// <summary>
    /// Character Studio: a small toy customisation room. One large Plop wearing the previewed look, a category
    /// row, a scrollable item grid and one main action (Equip / Unlock / Equipped / not enough Dust / milestone).
    /// Every item can be previewed before unlocking. Unlocks go through <see cref="CosmeticCollection"/> (one
    /// atomic wallet write). Preview atlases exist only while the Studio is open and are disposed on close.
    /// </summary>
    public sealed class CharacterStudio : MonoBehaviour
    {
        public event Action Closed;
        public event Action<CosmeticItem> Unlocked;
        public event Action ButtonPressed;

        internal enum ActionKind { Equipped, Equip, Unlock, Confirm, NeedDust, Milestone }

        private const float PreviewScale = 430f;
        private const int PreviewDensity = 320;
        private const int ThumbDensity = 128;
        private const float ThumbScale = 140f;
        private const float ConfirmSeconds = 3f;
        private const float TapCooldown = 0.5f;
        private static readonly CosmeticCategory[] Tabs =
        {
            CosmeticCategory.BodyStyle, CosmeticCategory.Palette, CosmeticCategory.BodyFinish, CosmeticCategory.Face,
            CosmeticCategory.Accessory, CosmeticCategory.Nest, CosmeticCategory.Effect, CosmeticCategory.Board
        };
        private static readonly string[] TabNames = { "Body", "Colors", "Finish", "Face", "Accessories", "Nest", "Effects", "Board" };
        private static readonly Reaction[] TapCycle =
            { Reaction.Bounce, Reaction.Surprised, Reaction.Wobble, Reaction.HappyBlink, Reaction.Squash, Reaction.Sleepy };

        private MenuKit _kit;
        private RectTransform _root, _safe, _room, _stage, _grid, _chipsContent;
        private CanvasGroup _group;
        private Text _dust, _itemName, _itemStatus, _actionLabel, _toast;
        private Image _actionIcon, _actionFill;
        private Button _actionButton;
        private readonly List<(Image Fill, Text Label)> _chips = new List<(Image, Text)>();
        private readonly List<(CosmeticItem Item, RectTransform Card, Image Fill, Image Badge, Text Price)> _cards =
            new List<(CosmeticItem, RectTransform, Image, Image, Text)>();

        private CosmeticCollection _collection;
        private ShapeAtlas _thumbs;
        private readonly Dictionary<string, ShapeAtlas> _previewAtlases = new Dictionary<string, ShapeAtlas>();
        private CharacterLook _equippedLook, _previewLook;
        private PlopView _preview;
        private MoveEffects _previewEffects;
        private Image _previewNest, _floor;
        private Color _floorBase;
        private CosmeticCategory _category = CosmeticCategory.BodyStyle;
        private CosmeticItem _selected;
        private ActionKind _action;
        private float _confirmUntil, _nextTap, _nextEffectDemo;
        private int _tapCount, _effectDirection = 1;
        private bool _reducedMotion, _batterySaver;
        private float _toastUntil;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;
        internal CharacterLook PreviewLook => _previewLook;
        internal CharacterLook EquippedLook => _equippedLook;
        internal CosmeticCategory Category => _category;
        internal CosmeticItem Selected => _selected;
        internal ActionKind Action => _action;
        internal string ActionText => _actionLabel != null ? _actionLabel.text : "";
        internal int VisibleItemCount => _cards.Count;
        internal PlopView PreviewPlop => _preview;
        internal float LastPreviewBakeMs { get; private set; }
        internal float ThumbnailBakeMs => _thumbs?.BakeMilliseconds ?? 0f;

        private void Awake()
        {
            _kit = new MenuKit();
            _root = MenuKit.Canvas(transform, "Character Studio Canvas", 60, out _group);
            MenuKit.Rect("Background", _root).gameObject.AddComponent<Image>().color =
                Color.Lerp(PlopPalette.PlayBackground, PlopPalette.Lavender, 0.10f);
            _safe = MenuKit.Rect("SafeArea", _root);
            MenuKit.FitSafeArea(_safe);

            // ---- header: Back · title · Dream Dust ----
            var back = _kit.Pill(_safe, "Studio Back", new Vector2(0f, 1f), new Vector2(110, -100), new Vector2(124, 116),
                PlopPalette.ButtonFill, null, HudIcons.Kind.Back, Close);
            back.GetComponentInChildren<Text>()?.gameObject.SetActive(false);
            var title = _kit.Label(_safe, "Studio Title", HudTheme.Display - 8, FontStyle.Bold, HudTheme.Ink, Vector2.zero, new Vector2(520, 110), _kit.Display);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -100));
            title.text = "Studio";
            var dustPill = MenuKit.Card(_safe, "Studio Dust", new Vector2(1f, 1f), new Vector2(-150, -100), new Vector2(236, 96), PlopPalette.ButtonFill);
            MenuKit.Glyph(dustPill, HudIcons.Kind.Star, new Vector2(-66, 0), 54, PlopPalette.Honey);
            _dust = _kit.Label(dustPill, "Dust amount", HudTheme.Action, FontStyle.Bold, HudTheme.Ink, new Vector2(24, 0), new Vector2(140, 70));

            // ---- preview room ----
            _room = MenuKit.Card(_safe, "Preview Room", new Vector2(0.5f, 1f), new Vector2(0, -500), new Vector2(980, 580),
                Color.Lerp(PlopPalette.ButtonFill, PlopPalette.Lavender, 0.08f));
            var floor = MenuKit.Card(_room, "Floor", new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(620, 120),
                Color.Lerp(PlopPalette.TrayRim, PlopPalette.ButtonFill, 0.35f));
            _floor = floor.GetComponent<Image>();
            _floor.raycastTarget = false;
            _floorBase = _floor.color;
            _stage = MenuKit.Rect("Stage", _room);
            var tap = MenuKit.Rect("Preview tap area", _room);
            tap.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            tap.gameObject.AddComponent<Button>().onClick.AddListener(PokePreview);

            _itemName = _kit.Label(_safe, "Item Name", HudTheme.Heading, FontStyle.Bold, HudTheme.Ink, Vector2.zero, new Vector2(900, 70), _kit.Display);
            Anchor(_itemName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -835));
            _itemStatus = _kit.Label(_safe, "Item Status", HudTheme.Caption, FontStyle.Bold, HudTheme.SecondaryText, Vector2.zero, new Vector2(900, 50));
            Anchor(_itemStatus.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -892));

            BuildChips();
            BuildGrid();

            // ---- main action ----
            var action = _kit.Pill(_safe, "Studio Action", new Vector2(0.5f, 0f), new Vector2(0, 120), new Vector2(660, 150),
                PlopPalette.ButtonFill, "Equip", HudIcons.Kind.Check, PressAction, HudTheme.Action + 4);
            _actionButton = action.GetComponent<Button>();
            _actionFill = action.GetComponent<Image>();
            _actionLabel = action.Find("Label").GetComponent<Text>();
            _actionIcon = action.Find("Icon").GetComponent<Image>();

            _toast = _kit.Label(_safe, "Studio Toast", HudTheme.Action, FontStyle.Bold, HudTheme.Ink, Vector2.zero, new Vector2(900, 60));
            Anchor(_toast.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 245));
            _toast.gameObject.SetActive(false);
            _root.gameObject.SetActive(false);
        }

        private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
        }

        private void BuildChips()
        {
            var viewport = MenuKit.Rect("Category Row", _safe);
            viewport.anchorMin = new Vector2(0f, 1f);
            viewport.anchorMax = new Vector2(1f, 1f);
            viewport.pivot = new Vector2(0.5f, 1f);
            viewport.sizeDelta = new Vector2(-40, 104);
            viewport.anchoredPosition = new Vector2(0, -930);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            _chipsContent = MenuKit.Rect("Chips", viewport);
            _chipsContent.anchorMin = _chipsContent.anchorMax = new Vector2(0f, 0.5f);
            _chipsContent.pivot = new Vector2(0f, 0.5f);
            float x = 10f;
            for (int i = 0; i < Tabs.Length; i++)
            {
                float w = 52f + TabNames[i].Length * 21f;
                int index = i;
                var chip = MenuKit.Rect("Chip " + TabNames[i], _chipsContent);
                chip.anchorMin = chip.anchorMax = new Vector2(0f, 0.5f);
                chip.pivot = new Vector2(0f, 0.5f);
                chip.sizeDelta = new Vector2(w, 88);
                chip.anchoredPosition = new Vector2(x, 0);
                var fill = chip.gameObject.AddComponent<Image>();
                fill.sprite = UiSprites.Pill;
                fill.type = Image.Type.Sliced;
                chip.gameObject.AddComponent<Button>().onClick.AddListener(() => { ButtonPressed?.Invoke(); SelectCategory(Tabs[index]); });
                chip.gameObject.AddComponent<PressScale>();
                var label = _kit.Label(chip, "Label", HudTheme.Caption, FontStyle.Bold, HudTheme.Ink, Vector2.zero, new Vector2(w, 80));
                label.text = TabNames[i];
                _chips.Add((fill, label));
                x += w + 14f;
            }
            _chipsContent.sizeDelta = new Vector2(x, 96);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = _chipsContent;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = viewport;
        }

        private void BuildGrid()
        {
            var viewport = MenuKit.Rect("Item Grid", _safe);
            viewport.offsetMin = new Vector2(40, 230);
            viewport.offsetMax = new Vector2(-40, -1050);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            _grid = MenuKit.Rect("Items", viewport);
            _grid.anchorMin = new Vector2(0f, 1f);
            _grid.anchorMax = new Vector2(1f, 1f);
            _grid.pivot = new Vector2(0.5f, 1f);
            _grid.sizeDelta = new Vector2(0, 0);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = _grid;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = viewport;
        }

        // ------------------------------------------------------------------ open / close

        public void Open(CosmeticCollection collection, bool reducedMotion, bool batterySaver)
        {
            _collection = collection;
            _reducedMotion = reducedMotion;
            _batterySaver = batterySaver;
            MenuKit.FitSafeArea(_safe);
            if (_thumbs == null) _thumbs = ShapeAtlas.StudioThumbnails(ThumbDensity);
            _equippedLook = CharacterArt.Resolve(collection.Equipped, collection.Catalog);
            _root.gameObject.SetActive(true);
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            SelectCategory(CosmeticCategory.BodyStyle);
        }

        public void Close()
        {
            if (!IsOpen) return;
            ButtonPressed?.Invoke();
            _root.gameObject.SetActive(false);
            ClearStage();
            foreach (var atlas in _previewAtlases.Values) atlas.Dispose();
            _previewAtlases.Clear();
            ClearCards();
            _thumbs?.Dispose();
            _thumbs = null;
            Closed?.Invoke();
        }

        // ------------------------------------------------------------------ selection

        internal void SelectCategory(CosmeticCategory category)
        {
            _category = category;
            for (int i = 0; i < Tabs.Length; i++)
            {
                bool on = Tabs[i] == category;
                _chips[i].Fill.color = on ? Color.Lerp(PlopPalette.Berry, PlopPalette.ButtonFill, 0.15f) : PlopPalette.ButtonFill;
                _chips[i].Label.color = on ? HudTheme.Ink : HudTheme.ActionInk;
            }
            ClearCards();
            var items = _collection.Catalog.InCategory(category);
            const int columns = 3;
            // Fits the narrowest supported canvas (20:9 phones give ~966 units of width).
            float cardW = 270f, cardH = 270f, gap = 16f;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                int row = i / columns, col = i % columns;
                var card = MenuKit.Card(_grid, "Item " + item.Id, new Vector2(0.5f, 1f),
                    new Vector2((col - 1) * (cardW + gap), -cardH * 0.5f - 14f - row * (cardH + gap)), new Vector2(cardW, cardH), PlopPalette.ButtonFill);
                var fill = card.GetComponent<Image>();
                var button = card.gameObject.AddComponent<Button>();
                button.onClick.AddListener(() => { ButtonPressed?.Invoke(); Select(item.Id); });
                card.gameObject.AddComponent<PressScale>();
                BuildThumb(card, item);
                var name = _kit.Label(card, "Name", HudTheme.Caption - 2, FontStyle.Bold, HudTheme.Ink, new Vector2(0, -70), new Vector2(cardW - 16, 44));
                name.text = item.Name;
                var price = _kit.Label(card, "Price", HudTheme.Caption - 6, FontStyle.Bold, HudTheme.SecondaryText, new Vector2(0, -106), new Vector2(cardW - 16, 36));
                var badge = MenuKit.Glyph(card, HudIcons.Kind.Check, new Vector2(cardW * 0.5f - 38f, cardH * 0.5f - 38f), 46, HudTheme.Ink);
                _cards.Add((item, card, fill, badge, price));
            }
            int rows = (items.Count + columns - 1) / columns;
            _grid.sizeDelta = new Vector2(0, rows * (cardH + gap) + 30f);
            _grid.anchoredPosition = Vector2.zero;

            var equipped = _collection.Catalog.Find(_collection.Equipped.Get(category)) ?? _collection.Catalog.Default(category);
            Select(equipped?.Id);
        }

        internal void Select(string id)
        {
            var item = _collection.Catalog.Find(id);
            if (item == null) return;
            if (item.Category != _category) SelectCategory(item.Category);
            _selected = item;
            _confirmUntil = 0f;
            SetPreview(CharacterArt.WithItem(_equippedLook, item));
            RefreshState();
        }

        private void RefreshState()
        {
            _dust.text = _collection.Wallet.Balance.ToString();
            foreach (var c in _cards)
            {
                var state = _collection.StateOf(c.Item);
                bool selected = _selected != null && c.Item.Id == _selected.Id;
                c.Fill.color = selected ? Color.Lerp(PlopPalette.ButtonFill, PlopPalette.Berry, 0.30f) : PlopPalette.ButtonFill;
                c.Card.localScale = selected ? new Vector3(1.03f, 1.03f, 1f) : Vector3.one;
                c.Badge.gameObject.SetActive(state != CosmeticState.Owned); // check when worn, lock when locked
                c.Badge.sprite = HudIcons.Get(state == CosmeticState.Equipped ? HudIcons.Kind.Check : HudIcons.Kind.Lock);
                c.Badge.color = state == CosmeticState.Equipped ? Color.Lerp(PlopPalette.Mint, PlopPalette.Ink, 0.45f) : HudTheme.DisabledText;
                c.Price.text = PriceText(c.Item, state);
            }
            if (_selected == null) return;
            var s = _collection.StateOf(_selected);
            _itemName.text = _selected.Name;
            _itemStatus.text = StatusText(_selected, s);
            switch (s)
            {
                case CosmeticState.Equipped: SetAction(ActionKind.Equipped, "Equipped", HudIcons.Kind.Check, false); break;
                case CosmeticState.Owned: SetAction(ActionKind.Equip, "Equip", HudIcons.Kind.Check, true); break;
                case CosmeticState.Affordable:
                    if (Time.unscaledTime < _confirmUntil) SetAction(ActionKind.Confirm, "Tap to confirm", HudIcons.Kind.Star, true);
                    else SetAction(ActionKind.Unlock, "Unlock " + _selected.Cost, HudIcons.Kind.Star, true);
                    break;
                case CosmeticState.TooExpensive:
                    SetAction(ActionKind.NeedDust, "Need " + _collection.Shortfall(_selected) + " more", HudIcons.Kind.Star, false);
                    break;
                default:
                    SetAction(ActionKind.Milestone, CosmeticCollection.ConditionText(_selected), HudIcons.Kind.Lock, false);
                    break;
            }
        }

        private static string PriceText(CosmeticItem item, CosmeticState state)
        {
            switch (state)
            {
                case CosmeticState.Equipped: return "Equipped";
                case CosmeticState.Owned: return item.IsFree ? "Free" : "Owned";
                case CosmeticState.MilestoneOnly: return CosmeticCollection.ConditionText(item);
                default:
                    return item.Unlock == UnlockMethod.Milestone
                        ? CosmeticCollection.ShortCondition(item) + " or " + item.Cost
                        : item.Cost + " Dust";
            }
        }

        private string StatusText(CosmeticItem item, CosmeticState state)
        {
            switch (state)
            {
                case CosmeticState.Equipped: return "Wearing now";
                case CosmeticState.Owned: return item.IsFree ? "Free · tap Equip" : "Unlocked";
                case CosmeticState.MilestoneOnly: return "Locked · " + CosmeticCollection.ConditionText(item);
                default:
                    string milestone = item.Unlock == UnlockMethod.Milestone ? CosmeticCollection.ConditionText(item) + " or " : "";
                    return "Locked · " + milestone + item.Cost + " Dream Dust";
            }
        }

        private void SetAction(ActionKind kind, string label, HudIcons.Kind icon, bool enabled)
        {
            _action = kind;
            _actionLabel.text = label;
            _actionIcon.sprite = HudIcons.Get(icon);
            _actionButton.interactable = enabled;
            Color fill = kind == ActionKind.Equip ? Color.Lerp(PlopPalette.Berry, PlopPalette.ButtonFill, 0.15f)
                : kind == ActionKind.Unlock || kind == ActionKind.Confirm ? Color.Lerp(PlopPalette.Honey, PlopPalette.ButtonFill, kind == ActionKind.Confirm ? 0.05f : 0.25f)
                : PlopPalette.ButtonFill;
            _actionFill.color = fill;
            _actionLabel.color = enabled ? HudTheme.Ink : HudTheme.DisabledText;
            _actionIcon.color = enabled ? HudTheme.ActionInk : HudTheme.DisabledText;
        }

        /// <summary>The main button. Unlocking takes two taps (Unlock, then confirm) so a stray tap never spends Dust.</summary>
        internal void PressAction()
        {
            if (_selected == null) return;
            ButtonPressed?.Invoke();
            float now = Time.unscaledTime;
            switch (_action)
            {
                case ActionKind.Equip:
                    if (_collection.Equip(_selected.Id))
                    {
                        _equippedLook = CharacterArt.Resolve(_collection.Equipped, _collection.Catalog);
                        ShowToast("Equipped");
                        _preview?.React(Reaction.HappyBlink, now);
                    }
                    break;
                case ActionKind.Unlock:
                    _confirmUntil = now + ConfirmSeconds;
                    break;
                case ActionKind.Confirm:
                    _confirmUntil = 0f;
                    var result = _collection.TryUnlock(_selected.Id, _selected.Category);
                    if (result == UnlockResult.Unlocked)
                    {
                        _collection.Equip(_selected.Id);
                        _equippedLook = CharacterArt.Resolve(_collection.Equipped, _collection.Catalog);
                        ShowToast("Unlocked " + _selected.Name + "!");
                        _preview?.React(Reaction.Bounce, now);
                        Unlocked?.Invoke(_selected);
                    }
                    else ShowToast(result == UnlockResult.InsufficientDust ? "Not enough Dream Dust" : "Could not unlock, try again");
                    break;
            }
            RefreshState();
        }

        private void ShowToast(string text)
        {
            _toast.text = text;
            _toast.gameObject.SetActive(true);
            _toastUntil = Time.unscaledTime + 1.6f;
        }

        // ------------------------------------------------------------------ preview

        private void SetPreview(CharacterLook look)
        {
            bool sideBySide = _category == CosmeticCategory.Nest;
            // Board decorations show on the preview floor (they tint, so no re-bake is needed).
            if (_floor != null && look.Board != null) _floor.color = BoardDecorTint.Blend(_floorBase, look.Board.Tray, look.Board.Amount * 1.3f);
            if (_preview != null && _previewLook != null && _previewLook.Key == look.Key && (_previewNest != null) == sideBySide) return;
            _previewLook = look;
            ClearStage();
            if (!_previewAtlases.TryGetValue(look.Key, out var atlas))
            {
                if (_previewAtlases.Count >= 4)
                {
                    foreach (var a in _previewAtlases.Values) a.Dispose();
                    _previewAtlases.Clear();
                }
                atlas = ShapeAtlas.Build(PreviewDensity, look, ShapeAtlas.Content.Characters | ShapeAtlas.Content.Nests, 0);
                _previewAtlases[look.Key] = atlas;
                LastPreviewBakeMs = atlas.BakeMilliseconds;
            }

            float scale = sideBySide ? PreviewScale * 0.78f : PreviewScale;
            float groundY = -200f;
            float plopX = sideBySide ? -200f : 0f;
            if (sideBySide)
            {
                _previewNest = Place(_stage, atlas.Get(ShapeAtlas.NestKey(0)), new Vector2(210f / scale, (groundY + 0.345f * scale) / scale), scale);
                _previewNest.name = "Preview Nest";
            }
            _preview = new PlopView(_stage, atlas, 0, 5, _reducedMotion, ui: true);
            _preview.Root.localScale = new Vector3(scale, scale, 1f);
            _preview.Snap(new Vector3(plopX, groundY + 0.345f * scale, 0f), false);
            // Effects live in the Plop's scaled space so their offsets are in cell units, like on the board.
            var fx = MenuKit.Rect("Preview Effects", _stage);
            fx.anchorMin = fx.anchorMax = new Vector2(0.5f, 0.5f);
            fx.sizeDelta = Vector2.zero;
            fx.anchoredPosition = new Vector2(plopX, groundY + 0.345f * scale);
            fx.localScale = new Vector3(scale, scale, 1f);
            _previewEffects = new MoveEffects(fx, atlas, true) { Enabled = !_reducedMotion, BatterySaver = _batterySaver };
            _nextEffectDemo = Time.unscaledTime + 0.25f;
        }

        private void ClearStage()
        {
            if (_stage == null) return;
            for (int i = _stage.childCount - 1; i >= 0; i--) Destroy(_stage.GetChild(i).gameObject);
            _preview = null;
            _previewEffects = null;
            _previewNest = null;
        }

        private void PokePreview()
        {
            float now = Time.unscaledTime;
            if (_preview == null || now < _nextTap) return;
            _nextTap = now + TapCooldown;
            _preview.React(TapCycle[_tapCount++ % TapCycle.Length], now);
        }

        private void Update()
        {
            if (!IsOpen) return;
            float now = Time.unscaledTime;
            _preview?.Tick(now);
            if (_previewEffects != null)
            {
                if (_category == CosmeticCategory.Effect && _previewLook.Effect.Kind != EffectKind.None && now >= _nextEffectDemo && !_reducedMotion)
                {
                    _nextEffectDemo = now + 1.1f;
                    _effectDirection = -_effectDirection;
                    _previewEffects.Spawn(Vector3.zero, new Vector2(_effectDirection, 0f), _previewLook.Palette.Toy(0), now, 0);
                    _preview.React(Reaction.Squash, now);
                }
                _previewEffects.Tick(now);
            }
            if (_action == ActionKind.Confirm && now >= _confirmUntil) RefreshState();
            if (_toast.gameObject.activeSelf && now >= _toastUntil) _toast.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ thumbnails

        private void ClearCards()
        {
            if (_grid != null)
                for (int i = _grid.childCount - 1; i >= 0; i--) Destroy(_grid.GetChild(i).gameObject);
            _cards.Clear();
        }

        private void BuildThumb(RectTransform card, CosmeticItem item)
        {
            var area = MenuKit.Rect("Thumb", card);
            area.anchorMin = area.anchorMax = new Vector2(0.5f, 0.5f);
            area.sizeDelta = new Vector2(210, 160);
            area.anchoredPosition = new Vector2(0, 40);
            var d = CharacterLook.Default;
            string asset = CosmeticCatalog.AssetName(item.Asset);
            switch (item.Category)
            {
                case CosmeticCategory.BodyStyle:
                    ThumbPlop(area, CharacterArt.Styles.TryGetValue(asset, out var style) ? style : d.Style, d.Finish, d.Palette, 0, d.Face, null, 0f, ThumbScale);
                    break;
                case CosmeticCategory.BodyFinish:
                    ThumbPlop(area, d.Style, CharacterArt.Finishes.TryGetValue(asset, out var finish) ? finish : d.Finish, d.Palette, 0, d.Face, null, 0f, ThumbScale);
                    break;
                case CosmeticCategory.Palette:
                {
                    var palette = CharacterArt.Palettes.TryGetValue(asset, out var p) ? p : d.Palette;
                    for (int c = 0; c < 3; c++) ThumbPlop(area, d.Style, d.Finish, palette, c, d.Face, null, (c - 1) * 66f, ThumbScale * 0.52f);
                    break;
                }
                case CosmeticCategory.Face:
                    ThumbPlop(area, d.Style, d.Finish, d.Palette, 0, CharacterArt.Faces.TryGetValue(asset, out var face) ? face : d.Face, null, 0f, ThumbScale);
                    break;
                case CosmeticCategory.Accessory:
                    ThumbPlop(area, d.Style, d.Finish, d.Palette, 0, d.Face,
                        CharacterArt.Accessories.TryGetValue(asset, out var acc) ? acc : null, 0f, ThumbScale);
                    break;
                case CosmeticCategory.Nest:
                    if (CharacterArt.Nests.TryGetValue(asset, out var nest) && _thumbs.Has(ShapeAtlas.ThumbNestKey(nest)))
                        Place(area, _thumbs.Get(ShapeAtlas.ThumbNestKey(nest)), new Vector2(0f, 0f), 165f);
                    break;
                case CosmeticCategory.Board:
                    BoardThumb(area, CharacterArt.Boards.TryGetValue(asset, out var decor) ? decor : d.Board);
                    break;
                case CosmeticCategory.Effect:
                    if (CharacterArt.Effects.TryGetValue(asset, out var fx) && _thumbs.Has(ShapeAtlas.ThumbEffectKey(fx)))
                    {
                        var sprite = _thumbs.Get(ShapeAtlas.ThumbEffectKey(fx));
                        Place(area, sprite, new Vector2(-0.30f, -0.10f), 260f);
                        Place(area, sprite, new Vector2(0.10f, 0.12f), 330f);
                        Place(area, sprite, new Vector2(0.28f, -0.18f), 200f);
                    }
                    else
                    {
                        ThumbPlop(area, d.Style, d.Finish, d.Palette, 0, d.Face, null, 0f, ThumbScale * 0.8f);
                    }
                    break;
            }
        }

        /// <summary>A still Plop made of thumbnail sprites (no animation, no per-frame cost).</summary>
        private void ThumbPlop(RectTransform parent, BodyStyle style, JellyFinish finish, CosmeticPalette palette, int colorId,
            FaceStyle face, AccessoryStyle accessory, float x, float scale)
        {
            var origin = new Vector2(x / scale, -70f / scale); // ground point
            Place(parent, _thumbs.Get(ShapeAtlas.ThumbShadowKey(style)), origin + new Vector2(0f, 0.005f), scale);
            string bodyKey = ShapeAtlas.ThumbBodyKey(style, finish, palette, colorId);
            if (!_thumbs.Has(bodyKey)) bodyKey = ShapeAtlas.ThumbBodyKey(style, CharacterLook.Default.Finish, CharacterLook.Default.Palette, 0);
            Place(parent, _thumbs.Get(bodyKey), origin, scale);
            var cheek = _thumbs.Get(ShapeAtlas.ThumbFaceKey(face, ShapeAtlas.CheekKey));
            Place(parent, cheek, origin + style.CheekL, scale);
            Place(parent, cheek, origin + style.CheekR, scale);
            Place(parent, _thumbs.Get(ShapeAtlas.ThumbFaceKey(face, ShapeAtlas.MouthKey(MouthShape.Neutral))), origin + style.Mouth, scale);
            var eye = _thumbs.Get(ShapeAtlas.ThumbFaceKey(face, ShapeAtlas.EyeKey(EyeShape.Open)));
            Place(parent, eye, origin + style.EyeL, scale);
            Place(parent, eye, origin + style.EyeR, scale);
            if (accessory != null && accessory.Anchor != AccessoryAnchor.None && _thumbs.Has(ShapeAtlas.ThumbAccessoryKey(accessory)))
            {
                Vector2 anchor = accessory.Anchor == AccessoryAnchor.Top ? style.TopAnchor : style.SideAnchor;
                var img = Place(parent, _thumbs.Get(ShapeAtlas.ThumbAccessoryKey(accessory)), origin + anchor + accessory.Offset, scale);
                img.rectTransform.localRotation = Quaternion.AngleAxis(accessory.Rotation, Vector3.forward);
            }
        }

        /// <summary>A tiny 3×2 board in the decor's colours over a neutral World tray.</summary>
        private static void BoardThumb(RectTransform parent, BoardDecor decor)
        {
            Color tray = BoardDecorTint.Blend(PlopPalette.Hex(0xE8DCCB), decor.Tray, decor.Amount);
            Color tile = BoardDecorTint.Blend(PlopPalette.Hex(0xFBF6EE), decor.Tile, decor.Amount);
            var panel = MenuKit.Rect("Board tray", parent);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(176f, 124f);
            var img = panel.gameObject.AddComponent<Image>();
            img.sprite = UiSprites.Card;
            img.type = Image.Type.Sliced;
            img.color = tray;
            img.raycastTarget = false;
            for (int y = 0; y < 2; y++)
            for (int x = 0; x < 3; x++)
            {
                var cell = MenuKit.Rect("Board tile", panel);
                cell.anchorMin = cell.anchorMax = new Vector2(0.5f, 0.5f);
                cell.sizeDelta = new Vector2(46f, 46f);
                cell.anchoredPosition = new Vector2((x - 1) * 52f, (y - 0.5f) * 52f);
                var ci = cell.gameObject.AddComponent<Image>();
                ci.sprite = UiSprites.Card;
                ci.type = Image.Type.Sliced;
                ci.color = tile * ((x + y) % 2 == 0 ? 1f : 0.965f);
                ci.pixelsPerUnitMultiplier = 3.2f; // keep the soft-square tile shape at thumbnail size
                ci.raycastTarget = false;
            }
        }

        /// <summary>Places a sprite so its pivot lands at <paramref name="units"/> (cell units × scale) inside parent.</summary>
        private static Image Place(RectTransform parent, Sprite sprite, Vector2 units, float scale)
        {
            var rt = MenuKit.Rect(sprite.name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
            rt.sizeDelta = sprite.rect.size / sprite.pixelsPerUnit * scale;
            rt.anchoredPosition = units * scale;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }
    }
}
