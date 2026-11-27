using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Plop.Presentation
{
    /// <summary>
    /// Sprint 04 advertising UI on the HUD: the voluntary offer card (exact reward stated, every option the same size and
    /// colour, "No, thanks" always present, Back declines) and the Settings ▸ Privacy Choices row shown only when the
    /// consent SDK requires it. Nothing here draws over an SDK ad.
    /// </summary>
    public sealed partial class GameHud
    {
        public event Action PrivacyChoicesClicked;

        private const int AdOfferSlots = 3;
        private static readonly Vector2 AdOfferSize = new Vector2(760, 640);
        private static readonly Vector2 SettingsBaseSize = new Vector2(840, 1342);
        private static readonly Vector2 SettingsPrivacySize = new Vector2(840, 1454);
        private RectTransform _adOffer, _adOfferPanel;
        private Text _adOfferTitle, _adOfferMessage;
        private readonly Button[] _adOfferButtons = new Button[AdOfferSlots];
        private readonly Text[] _adOfferLabels = new Text[AdOfferSlots];
        private readonly GameObject[] _adOfferTags = new GameObject[AdOfferSlots];
        private readonly Action[] _adOfferActions = new Action[AdOfferSlots];
        private Action _adOfferDecline;
        private Button _privacyRow;
        private bool _privacyVisible;

        public bool IsAdOfferOpen => _adOffer != null && _adOffer.gameObject.activeSelf;
        public bool PrivacyChoicesVisible => _privacyVisible;
        private Vector2 SettingsSize => _privacyVisible ? SettingsPrivacySize : SettingsBaseSize;

        /// <summary>
        /// Opens the offer card. <paramref name="options"/> (1–2 choices) are followed by "No, thanks", which runs
        /// <paramref name="decline"/>. Every choice closes the card first.
        /// </summary>
        public void ShowAdOffer(string title, string message, IReadOnlyList<AdOfferOption> options, Action decline)
        {
            if (_adOffer == null || options == null || options.Count == 0 || options.Count > AdOfferSlots - 1) return;
            Loc.SetRaw(_adOfferTitle, title);
            Loc.SetRaw(_adOfferMessage, message);
            for (int i = 0; i < AdOfferSlots; i++)
            {
                bool decline_ = i == options.Count;
                bool used = i <= options.Count;
                _adOfferButtons[i].gameObject.SetActive(used);
                var shadow = _adOfferButtons[i].transform.parent.Find(_adOfferButtons[i].name.Replace(" Button", " Shadow"));
                if (shadow != null) shadow.gameObject.SetActive(used);
                if (!used) continue;
                var option = decline_ ? new AdOfferOption(Loc.T("No, thanks"), null) : options[i];
                Loc.SetRaw(_adOfferLabels[i], option.Label);
                _adOfferButtons[i].interactable = option.Enabled;
                _adOfferTags[i].SetActive(option.IsAd);
                _adOfferActions[i] = decline_ ? null : option.Chosen;
            }
            _adOfferDecline = decline;
            FitModal(_adOfferPanel, AdOfferSize);
            SetModal(_adOffer, true);
        }

        /// <summary>Closes the card without choosing (navigation, suspension).</summary>
        public void CloseAdOffer()
        {
            if (!IsAdOfferOpen) return;
            for (int i = 0; i < AdOfferSlots; i++) _adOfferActions[i] = null;
            _adOfferDecline = null;
            SetModal(_adOffer, false);
        }

        private void ChooseAdOffer(int slot)
        {
            if (!IsAdOfferOpen) return;
            var action = _adOfferActions[slot];
            var decline = _adOfferDecline;
            bool isDecline = action == null;
            CloseAdOffer();
            if (isDecline) decline?.Invoke();
            else action();
        }

        private void DeclineAdOffer()
        {
            var decline = _adOfferDecline;
            CloseAdOffer();
            decline?.Invoke();
        }

        private void BuildAdOffer()
        {
            _adOffer = BuildModal("Ad offer", AdOfferSize, out _adOfferPanel);
            var top = new Vector2(0.5f, 1f);
            _adOfferTitle = MakeText(_adOfferPanel, "Ad offer title", HudTheme.Heading, FontStyle.Bold, HudTheme.Ink,
                top, new Vector2(0, -84), new Vector2(680, 76));
            _adOfferMessage = MakeText(_adOfferPanel, "Ad offer message", HudTheme.Caption, FontStyle.Bold, HudTheme.SecondaryText,
                top, new Vector2(0, -178), new Vector2(680, 104));
            _adOfferMessage.horizontalOverflow = HorizontalWrapMode.Wrap; // QA: the message ran past the card edges
            _adOfferMessage.resizeTextForBestFit = true;
            _adOfferMessage.resizeTextMaxSize = HudTheme.Caption;
            _adOfferMessage.resizeTextMinSize = 20;
            for (int i = 0; i < AdOfferSlots; i++)
            {
                int slot = i;
                // Same size and colour for every option: the offer never pushes the ad choice.
                var button = MakeButton(_adOfferPanel, "Offer " + i, new Vector2(0, -300 - i * 112), new Vector2(620, 92),
                    PlopPalette.Milk, () => ChooseAdOffer(slot), top, fontSize: HudTheme.Control, touchHeight: 104f);
                _adOfferButtons[i] = button;
                _adOfferLabels[i] = button.GetComponentInChildren<Text>();
                Loc.SetRaw(_adOfferLabels[i], ""); // filled with already-localized text when the card opens
                _adOfferLabels[i].resizeTextForBestFit = true;
                _adOfferLabels[i].resizeTextMaxSize = HudTheme.Control;
                _adOfferLabels[i].resizeTextMinSize = 18;
                var tag = MakeBadge(button.transform, "Ad tag", new Vector2(72, 44), PlopPalette.ButtonFill);
                tag.anchoredPosition = new Vector2(-262, 0);
                Loc.Set(MakeText(tag, "Ad tag label", 24, FontStyle.Bold, HudTheme.Ink, Vector2.one * 0.5f, Vector2.zero,
                    new Vector2(70, 40)), "Ad");
                _adOfferTags[i] = tag.gameObject;
            }
            _adOffer.gameObject.SetActive(false);
        }

        /// <summary>Settings ▸ Privacy Choices: opens the consent SDK's own privacy options form.</summary>
        private void BuildPrivacyRow(RectTransform panel)
        {
            _privacyRow = MakeButton(panel, "Privacy Choices", new Vector2(0, -1354), new Vector2(460, HudTheme.ModalButtonHeight),
                PlopPalette.Milk, () =>
                {
                    ModalInteracted?.Invoke();
                    PrivacyChoicesClicked?.Invoke();
                }, new Vector2(0.5f, 1f), fontSize: HudTheme.Control);
            SetPrivacyChoicesVisible(false);
        }

        /// <summary>Shows the row only when the consent SDK says a privacy entry point is required.</summary>
        public void SetPrivacyChoicesVisible(bool visible)
        {
            _privacyVisible = visible;
            if (_privacyRow != null)
            {
                _privacyRow.gameObject.SetActive(visible);
                var shadow = _privacyRow.transform.parent.Find("Privacy Choices Shadow");
                if (shadow != null) shadow.gameObject.SetActive(visible);
            }
            if (_settingsPanel != null)
            {
                _settingsPanel.sizeDelta = SettingsSize;
                FitModal(_settingsPanel, SettingsSize);
            }
        }
    }
}
