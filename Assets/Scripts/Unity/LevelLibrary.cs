using System.Collections.Generic;
using Plop.Core;
using Plop.Solver;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Loads the shipped level pack from Resources (Assets/Levels/Resources/Levels/*.json).
    /// Runtime loading skips the developer-only "dev" block. No generation happens at runtime.
    /// </summary>
    public static class LevelLibrary
    {
        /// <summary>Phase 11 rebalanced campaign (160 levels, nine Worlds).</summary>
        public const string DefaultPack = "Levels/PLOP_Pack_003";
        /// <summary>The Phase 10 campaign. Archived; its progress key is read once for migration.</summary>
        public const string PreviousPack = "Levels/PLOP_Pack_002";
        /// <summary>The Phase 8 campaign. Archived; its progress key is read once for migration.</summary>
        public const string LegacyPack = "Levels/PLOP_Pack_001";
        /// <summary>Level ids of <see cref="PreviousPack"/> in campaign order (migration only).</summary>
        public const string PreviousOrder = "Levels/PLOP_Pack_002_Order";
        public const string DailyPack = "Levels/PLOP_Daily_002";
        public const string DeadlockPack = "Levels/PLOP_Deadlocks_003";

        /// <summary>True for a pack resource that is no longer shipped (scenes saved before a rebuild).</summary>
        public static bool IsRetired(string resource) => resource == PreviousPack || resource == LegacyPack;

        public static DeadlockCatalog LoadDeadlocks()
        {
            var asset = Resources.Load<TextAsset>(DeadlockPack);
            if (asset == null) return null;
            bool valid = DeadlockCatalog.TryRead(asset.bytes, out var catalog);
            Resources.UnloadAsset(asset);
            if (valid) PlopLog.Info("Loaded deadlock proofs for " + catalog.LevelCount + " levels.");
            else PlopLog.Info("Deadlock proof data unreadable. Bounded search remains available.");
            return valid ? catalog : null;
        }

        public static List<LevelDefinition> Load(string resourcePath = DefaultPack)
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                PlopLog.Error("Level pack not found at Resources/" + resourcePath);
                return new List<LevelDefinition>();
            }
            var levels = LevelSerializer.PackFromJson(asset.text, includeDevData: false);
            Resources.UnloadAsset(asset);
            return levels;
        }
    }

    /// <summary>Logging that compiles away in release player builds (no log spam on device).</summary>
    public static class PlopLog
    {
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void Info(string message) => Debug.Log("[PLOP] " + message);

        public static void Error(string message) => Debug.LogError("[PLOP] " + message);
    }
}
