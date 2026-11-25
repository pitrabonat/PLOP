using System;
using Plop.Meta;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>Loads the shipped cosmetic catalog. A missing or broken file falls back to the free defaults only.</summary>
    public static class CosmeticCatalogLoader
    {
        /// <summary>Minimal catalog: every category's default, all free. Used only if the data file is unusable.</summary>
        internal const string FallbackJson = @"{""schemaVersion"":1,""contentVersion"":1,
          ""defaults"":{""BodyStyle"":""body.classic"",""BodyFinish"":""finish.classic"",""Palette"":""palette.classic"",""Face"":""face.classic"",
                       ""Accessory"":""accessory.none"",""Nest"":""nest.classic"",""Effect"":""effect.none""},
          ""items"":[
            {""id"":""body.classic"",""category"":""BodyStyle"",""name"":""Classic"",""asset"":""style:classic"",""unlock"":""Free""},
            {""id"":""finish.classic"",""category"":""BodyFinish"",""name"":""Classic Jelly"",""asset"":""finish:classic"",""unlock"":""Free""},
            {""id"":""palette.classic"",""category"":""Palette"",""name"":""Toy Box"",""asset"":""palette:classic"",""unlock"":""Free""},
            {""id"":""face.classic"",""category"":""Face"",""name"":""Sweet"",""asset"":""face:classic"",""unlock"":""Free""},
            {""id"":""accessory.none"",""category"":""Accessory"",""name"":""None"",""asset"":""accessory:none"",""unlock"":""Free""},
            {""id"":""nest.classic"",""category"":""Nest"",""name"":""Moon Pillow"",""asset"":""nest:classic"",""unlock"":""Free""},
            {""id"":""effect.none"",""category"":""Effect"",""name"":""None"",""asset"":""effect:none"",""unlock"":""Free""}]}";

        private static CosmeticCatalog _cached;

        public static CosmeticCatalog Load()
        {
            if (_cached != null) return _cached;
            var asset = Resources.Load<TextAsset>(CosmeticCatalog.ResourcePath);
            try
            {
                if (asset == null) throw new FormatException("missing " + CosmeticCatalog.ResourcePath);
                var catalog = CosmeticCatalog.Parse(asset.text);
                var issues = catalog.Validate(CharacterArt.HasAsset);
                if (issues.Count > 0) Debug.LogWarning("[PLOP] Cosmetic catalog issues:\n" + string.Join("\n", issues));
                _cached = catalog;
            }
            catch (FormatException e)
            {
                Debug.LogError("[PLOP] Cosmetic catalog unusable, using free defaults: " + e.Message);
                _cached = CosmeticCatalog.Parse(FallbackJson);
            }
            return _cached;
        }
    }
}
