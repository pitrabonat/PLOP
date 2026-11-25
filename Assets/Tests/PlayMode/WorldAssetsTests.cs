using NUnit.Framework;
using Plop.Presentation;
using UnityEngine;

namespace Plop.Tests.PlayMode
{
    public sealed class WorldAssetsTests
    {
        // Phase 10: nine Worlds over 160 levels; the five original ids are unchanged.
        private static readonly string[] Ids =
        {
            "cozy-nest", "frosted-pond", "breezy-meadow", "ember-hollow", "prism-observatory",
            "dewdrop-garden", "rainy-window", "twilight-attic", "moonlit-clouds"
        };
        private static readonly int[] First = { 1, 7, 21, 35, 49, 64, 86, 109, 134 };
        private static readonly int[] Last = { 6, 20, 34, 48, 63, 85, 108, 133, 160 };
        private static readonly WorldAmbience[] Beds =
        {
            WorldAmbience.CozyNest, WorldAmbience.FrostedPond, WorldAmbience.BreezyMeadow, WorldAmbience.EmberHollow,
            WorldAmbience.PrismObservatory, WorldAmbience.DewdropGarden, WorldAmbience.RainyWindow,
            WorldAmbience.TwilightAttic, WorldAmbience.MoonlitClouds
        };

        [Test]
        public void ProductionWorldsHaveStableRangesAndImportedArt()
        {
            WorldCatalog catalog = WorldCatalog.Load();
            Assert.AreEqual(Ids.Length, catalog.Worlds.Count);

            for (int i = 0; i < Ids.Length; i++)
            {
                WorldTheme theme = catalog.Worlds[i];
                Assert.AreEqual(Ids[i], theme.Id);
                Assert.AreEqual(First[i], theme.FirstLevel);
                Assert.AreEqual(Last[i], theme.LastLevel);
                Assert.AreSame(theme, catalog.ForLevelIndex(theme.FirstLevel - 1));
                Assert.AreSame(theme, catalog.ForLevelIndex(theme.LastLevel - 1));
                Assert.AreEqual(Beds[i], theme.Ambience);
                Assert.IsNotNull(theme.Preview, theme.Id + " preview must resolve to the imported sprite.");
                Assert.AreEqual(new Vector2(320f, 160f), theme.Preview.rect.size);

                Sprite backdrop = Resources.Load<Sprite>("WorldBackdrops/" + theme.Id);
                Assert.IsNotNull(backdrop, theme.Id + " background must import as a sprite.");
                Assert.AreEqual(new Vector2(576f, 1024f), backdrop.rect.size);
            }

            Assert.IsNull(catalog.ForLevelIndex(-1));
            Assert.IsNull(catalog.ForLevelIndex(160));
            Assert.IsNull(WorldCatalog.Empty.ForLevelIndex(0));
        }

        [Test]
        public void PortraitBackdropCoversTheCameraWithoutMovingTheGameplayRoot()
        {
            var root = new GameObject("Gameplay root");
            var cameraObject = new GameObject("World camera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.aspect = 1080f / 2400f;
                camera.transform.position = new Vector3(1f, 2f, -10f);
                var layout = BoardLayout.Compute(1080, 2400, new Rect(0, 80, 1080, 2240), 5, 5);
                camera.orthographicSize = layout.OrthographicSize;
                var backdrop = root.AddComponent<WorldBackdrop>();

                backdrop.Show(WorldCatalog.Load().ForLevelIndex(0), camera, layout);

                var renderer = root.GetComponentInChildren<SpriteRenderer>();
                Assert.IsNotNull(renderer);
                Assert.IsTrue(renderer.enabled);
                Assert.AreEqual(Vector3.zero, root.transform.position);
                Assert.AreEqual(new Vector3(1f, 2f, -10f), camera.transform.position);
                Assert.Less(renderer.sortingOrder, -1000);
                Assert.GreaterOrEqual(renderer.bounds.size.y, camera.orthographicSize * 2f);
                Assert.GreaterOrEqual(renderer.bounds.size.x, camera.orthographicSize * 2f * camera.aspect);

                backdrop.Show(null, camera, layout);
                Assert.IsFalse(renderer.enabled);
                Assert.IsNull(renderer.sprite);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(root);
            }
        }
    }
}
