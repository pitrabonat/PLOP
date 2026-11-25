using System;
using System.Collections.Generic;

namespace Plop.Meta
{
    /// <summary>Saved progress used for milestone unlocks. Built by the game from existing progress records.</summary>
    public struct ProgressSnapshot
    {
        public int LevelsCompleted;
        public int PerfectLevels;
        public int WorldsCompleted;
        public int DailyPuzzles;

        public int Count(MilestoneKind kind)
        {
            switch (kind)
            {
                case MilestoneKind.LevelsCompleted: return LevelsCompleted;
                case MilestoneKind.PerfectLevels: return PerfectLevels;
                case MilestoneKind.WorldsCompleted: return WorldsCompleted;
                case MilestoneKind.DailyPuzzles: return DailyPuzzles;
                default: return 0;
            }
        }
    }

    public enum UnlockResult { Unlocked, UnknownItem, WrongCategory, AlreadyOwned, NotForSale, InsufficientDust, SaveFailed }

    /// <summary>What the Studio shows for an item.</summary>
    public enum CosmeticState { Equipped, Owned, Affordable, TooExpensive, MilestoneOnly }

    /// <summary>
    /// The player's cosmetics: ownership (free items, milestone grants and Dust purchases recorded in the
    /// existing <see cref="DreamDustWallet"/>), purchases and the equipped preset. All rules are local,
    /// deterministic and visual-only: no random rewards, no paid currency, no gameplay effect.
    /// </summary>
    public sealed class CosmeticCollection
    {
        public CosmeticCatalog Catalog { get; }
        public DreamDustWallet Wallet { get; }
        public AppearanceStore Appearance { get; }
        public ProgressSnapshot Progress { get; private set; }

        public event Action Changed;

        public CosmeticCollection(CosmeticCatalog catalog, DreamDustWallet wallet, AppearanceStore appearance)
        {
            Catalog = catalog;
            Wallet = wallet;
            Appearance = appearance;
            Appearance.Sanitize(IsOwned);
        }

        public CharacterPreset Equipped => Appearance.Active;

        public bool IsOwned(CosmeticItem item)
            => item != null && (item.IsFree || Wallet.Owns(item.Id) || MilestoneMet(item));

        public bool IsOwned(string id) => IsOwned(Catalog.Find(id));

        public bool MilestoneMet(CosmeticItem item)
            => item.Unlock == UnlockMethod.Milestone && Progress.Count(item.Milestone) >= item.MilestoneCount;

        /// <summary>Updates progress and records ownership of every milestone item it now satisfies (once).</summary>
        public List<CosmeticItem> UpdateProgress(ProgressSnapshot progress)
        {
            Progress = progress;
            var fresh = new List<CosmeticItem>();
            foreach (var item in Catalog.Items)
                if (MilestoneMet(item) && !Wallet.Owns(item.Id)) fresh.Add(item);
            if (fresh.Count > 0)
            {
                var ids = new List<string>();
                foreach (var item in fresh) ids.Add(item.Id);
                Wallet.GrantOwnership(ids);
                Changed?.Invoke();
            }
            return fresh;
        }

        public CosmeticState StateOf(CosmeticItem item)
        {
            if (Equipped.Get(item.Category) == item.Id) return CosmeticState.Equipped;
            if (IsOwned(item)) return CosmeticState.Owned;
            if (!item.CanBuy) return CosmeticState.MilestoneOnly;
            return Wallet.Balance >= item.Cost ? CosmeticState.Affordable : CosmeticState.TooExpensive;
        }

        public int Shortfall(CosmeticItem item) => Math.Max(0, item.Cost - Wallet.Balance);

        /// <summary>
        /// Validates and performs a Dust unlock: item exists (and, if given, is in <paramref name="category"/>),
        /// is not owned, is for sale and affordable. Spend and ownership are saved in a single wallet write.
        /// </summary>
        public UnlockResult TryUnlock(string id, CosmeticCategory? category = null)
        {
            var item = Catalog.Find(id);
            if (item == null) return UnlockResult.UnknownItem;
            if (category.HasValue && item.Category != category.Value) return UnlockResult.WrongCategory;
            if (IsOwned(item)) return UnlockResult.AlreadyOwned;
            if (!item.CanBuy) return UnlockResult.NotForSale;
            if (Wallet.Balance < item.Cost) return UnlockResult.InsufficientDust;
            try
            {
                if (!Wallet.TryPurchase(item.Id, item.Cost)) return UnlockResult.InsufficientDust;
            }
            catch (Exception)
            {
                return UnlockResult.SaveFailed;
            }
            Changed?.Invoke();
            return UnlockResult.Unlocked;
        }

        /// <summary>Equips an owned item that fits the current body style. Returns false otherwise.</summary>
        public bool Equip(string id)
        {
            var item = Catalog.Find(id);
            if (item == null || !IsOwned(item)) return false;
            var preset = Equipped.Clone();
            preset.Set(item.Category, item.Id);
            if (item.Category == CosmeticCategory.BodyStyle)
            {
                // Keep every other slot valid for the new body style.
                foreach (var c in CosmeticCatalog.Categories)
                {
                    var other = Catalog.Find(preset.Get(c));
                    if (other != null && !other.CompatibleWith(item.Id)) preset.Set(c, Catalog.DefaultId(c));
                }
            }
            else if (!item.CompatibleWith(preset.Get(CosmeticCategory.BodyStyle))) return false;
            if (preset.SameAs(Equipped)) return true;
            Appearance.SetActive(preset);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Card-sized condition: "20 levels", "10 Perfects", "1 World", "3 Dailies".</summary>
        public static string ShortCondition(CosmeticItem item)
        {
            if (item.Unlock != UnlockMethod.Milestone) return "";
            int n = item.MilestoneCount;
            switch (item.Milestone)
            {
                case MilestoneKind.LevelsCompleted: return n + (n == 1 ? " level" : " levels");
                case MilestoneKind.PerfectLevels: return n + (n == 1 ? " Perfect" : " Perfects");
                case MilestoneKind.WorldsCompleted: return n + (n == 1 ? " World" : " Worlds");
                case MilestoneKind.DailyPuzzles: return n + (n == 1 ? " Daily" : " Dailies");
                default: return "";
            }
        }

        /// <summary>Short unlock condition for UI: "Complete 20 levels", "Earn 10 Perfects", …</summary>
        public static string ConditionText(CosmeticItem item)
        {
            if (item.Unlock != UnlockMethod.Milestone) return "";
            int n = item.MilestoneCount;
            switch (item.Milestone)
            {
                case MilestoneKind.LevelsCompleted: return "Complete " + n + (n == 1 ? " level" : " levels");
                case MilestoneKind.PerfectLevels: return "Earn " + n + (n == 1 ? " Perfect" : " Perfects");
                case MilestoneKind.WorldsCompleted: return "Finish " + n + (n == 1 ? " World" : " Worlds");
                case MilestoneKind.DailyPuzzles: return "Solve " + n + (n == 1 ? " Daily" : " Dailies");
                default: return "";
            }
        }
    }
}
