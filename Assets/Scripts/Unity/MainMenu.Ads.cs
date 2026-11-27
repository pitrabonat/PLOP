using System;
using UnityEngine;
using UnityEngine.UI;

namespace Plop.Presentation
{
    /// <summary>
    /// Sprint 04: the voluntary "Bonus Dream Dust" entry on the main menu. Hidden when ads are disabled or today's limit
    /// is reached; shown as "Ad unavailable" (not tappable) when no ad is loaded. Tapping it only opens a card that
    /// states the exact reward, with "Watch Ad" and "No, thanks" at the same size and colour. Nothing auto-plays.
    /// </summary>
    public sealed partial class MainMenu
    {
        /// <summary>The player chose "Watch Ad" on the bonus card.</summary>
        public event Action BonusDustRequested;

        private RectTransform _bonusPill, _bonusPillShadow, _bonusCard;
        private Button _bonusButton;
        private Text _bonusLabel, _bonusMessage;
        private int _bonusAmount, _bonusCap;

        public bool BonusVisible => _bonusPill != null && _bonusPill.gameObject.activeSelf;
        public bool BonusReady => BonusVisible && _bonusButton.interactable;
        public bool IsBonusCardOpen => _bonusCard != null && _bonusCard.gameObject.activeSelf;

        private void BuildBonusOffer()
        {
            _bonusPill = _kit.Pill(_safe, "Bonus Dream Dust", new Vector2(0.5f, 0.5f), new Vector2(0, -800), new Vector2(630, 100),
                PlopPalette.ButtonFill, "Bonus Dream Dust", HudIcons.Kind.Star, OpenBonusCard, HudTheme.Control);
            _bonusPillShadow = (RectTransform)_bonusPill.parent.Find("Bonus Dream Dust Shadow");
            _bonusButton = _bonusPill.GetComponent<Button>();
            _bonusLabel = _bonusPill.Find("Label").GetComponent<Text>();
            _bonusLabel.resizeTextForBestFit = true;
            _bonusLabel.resizeTextMaxSize = HudTheme.Control;
            _bonusLabel.resizeTextMinSize = 18;

            // Confirmation card over the menu.
            _bonusCard = MenuKit.Rect("Bonus card", _root);
            _bonusCard.gameObject.AddComponent<Image>().color = HudTheme.Ink.WithAlpha(0.18f);
            var card = MenuKit.Card(_bonusCard, "Bonus card panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720, 560), PlopPalette.ButtonFill);
            var title = _kit.Label(card, "Bonus title", HudTheme.Heading, FontStyle.Bold, HudTheme.Ink, new Vector2(0, 196), new Vector2(660, 76));
            Loc.Set(title, "Bonus Dream Dust");
            _bonusMessage = _kit.Label(card, "Bonus message", HudTheme.Caption, FontStyle.Bold, HudTheme.SecondaryText, new Vector2(0, 86), new Vector2(640, 120));
            _bonusMessage.horizontalOverflow = HorizontalWrapMode.Wrap; // QA: long translations stayed on one line past the card
            _bonusMessage.resizeTextForBestFit = true;
            _bonusMessage.resizeTextMaxSize = HudTheme.Caption;
            _bonusMessage.resizeTextMinSize = 20;
            // Same size and colour: the card never pushes the ad.
            _kit.Pill(card, "Watch Ad", new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(560, 100), PlopPalette.Milk,
                "Watch Ad", HudIcons.Kind.Play, ConfirmBonus, HudTheme.Control);
            _kit.Pill(card, "No thanks", new Vector2(0.5f, 0.5f), new Vector2(0, -184), new Vector2(560, 100), PlopPalette.Milk,
                "No, thanks", HudIcons.Kind.Back, () => CloseBonusCard(), HudTheme.Control);
            _bonusCard.gameObject.SetActive(false);
            SetBonusOffer(false, false, 0, 0);
        }

        /// <summary>
        /// <paramref name="visible"/>: ads are on and today's limit is not reached. <paramref name="ready"/>: an ad is
        /// loaded right now. The label states the exact reward or that no ad is available.
        /// </summary>
        public void SetBonusOffer(bool visible, bool ready, int amount, int dailyCap)
        {
            if (_bonusPill == null) return;
            _bonusAmount = amount;
            _bonusCap = dailyCap;
            _bonusPill.gameObject.SetActive(visible);
            if (_bonusPillShadow != null) _bonusPillShadow.gameObject.SetActive(visible);
            _bonusButton.interactable = visible && ready;
            if (ready) Loc.Set(_bonusLabel, "Watch Ad · +{0} Dream Dust", amount);
            else Loc.Set(_bonusLabel, "Ad unavailable");
            if (!visible || !ready) CloseBonusCard();
        }

        private void OpenBonusCard()
        {
            if (!BonusReady) return;
            Loc.Set(_bonusMessage, "Watch a short ad to get {0} Dream Dust. Up to {1} bonuses a day.", _bonusAmount, _bonusCap);
            _bonusCard.gameObject.SetActive(true);
            _bonusCard.SetAsLastSibling();
        }

        /// <summary>Back or "No, thanks": closes the card, nothing else happens.</summary>
        public bool CloseBonusCard()
        {
            if (!IsBonusCardOpen) return false;
            _bonusCard.gameObject.SetActive(false);
            return true;
        }

        private void ConfirmBonus()
        {
            if (!IsBonusCardOpen) return;
            _bonusCard.gameObject.SetActive(false);
            BonusDustRequested?.Invoke();
        }
    }
}
