using System.Collections.Generic;
using Plop.Core;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Loads the shipped level pack from Resources (Assets/Levels/Resources/Levels/*.json).
    /// Runtime loading skips the developer-only "dev" block. No generation happens at runtime.
    /// </summary>
    public static class LevelLibrary
    {
        public const string DefaultPack = "Levels/PLOP_Pack_001";

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
